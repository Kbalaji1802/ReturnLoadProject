# 03 — TECHNICAL BIBLE

> The **how**. Architecture, technology choices, standards, and environments.
>
> **Status:** DRAFT — proposed foundation, pending co-founder approval. Nothing
> here is locked until it appears as an accepted entry in `06_DECISION_LOG.md`.

---

## 1. Architecture at a glance

A clean, service-oriented architecture around a central **API backend**, with
dedicated clients for each audience and an isolated matching capability.

```
                +---------------------+
   Drivers ---> |   Mobile App        |\
                |   (/mobile)         | \
                +---------------------+  \
                                          \      +------------------------+
   Admins ----> +---------------------+    \---> |   Backend API          |
                |   Admin Web         |--------> |   (/backend)           |
                |   (/admin)          |    /---> |  auth, loads, trips,   |
                +---------------------+   /      |  matching, bookings,   |
                                          /       |  settlement, ratings   |
   Shippers --> +---------------------+  /        +-----------+------------+
                |   Mobile / Web      | /                     |
                +---------------------+/                      |
                                                              v
                          +----------------+        +---------------------+
                          | Matching / AI  |<------>|   Database          |
                          | (/ai + engine) |        |   (/database)       |
                          +----------------+        +---------------------+
```

## 2. Proposed technology stack

> These are **recommendations** to be confirmed in the Decision Log. Chosen for
> being production-proven, well-documented, and hire-able.

| Layer | Proposed choice | Why |
|-------|-----------------|-----|
| **Mobile** (`/mobile`) | **Flutter** | One codebase for iOS + Android, great for map/GPS-heavy driver apps, fast iteration. |
| **Admin web** (`/admin`) | **Angular** | Batteries-included, opinionated, strong for data-dense internal tooling. |
| **Backend API** (`/backend`) | **ASP.NET Core on .NET 9**, Clean Architecture (*ADR-0006*) | Strongly-typed, batteries-included, mature ecosystem; good concurrency for real-time matching. |
| **Database** (`/database`) | **PostgreSQL** (+ **PostGIS** for geo) | Rock-solid relational core; PostGIS is ideal for lanes, routes, and radius matching. |
| **Caching / realtime** | *TBD — e.g. Redis* | Hot lookups, geo queries, rate limiting, pub/sub for live updates. |
| **Matching / AI** (`/ai`) | *TBD* | Route overlap + time-window scoring; evolves from rules → ML ranking. |
| **Infra** (`/docker`) | **Docker + Docker Compose** (local), container orchestration in prod | Reproducible environments from laptop to production. |
| **API contract** | **OpenAPI / schema-first** | Single source of truth shared by all clients. |

> ⚠️ **Backend language/framework, caching layer, and cloud provider are NOT yet
> decided.** They are open decisions to be recorded in `06_DECISION_LOG.md`.

## 3. Repository layout (monorepo)

```
/ai        AI knowledge base + matching intelligence (this brain).
/backend   The API server: business logic, auth, persistence.
/mobile    Flutter app for drivers (and shippers).
/admin     Angular internal operations console.
/database  Schema, migrations, seed data, ERDs.
/docs      Human-facing documentation (architecture, API, runbooks).
/docker    Dockerfiles, docker-compose, container/orchestration config.
/scripts   Automation: setup, build, deploy, data, maintenance.
```

## 4. Core domain services (inside /backend)

Logical modules (not necessarily separate deployables at day one):

- **Identity & Access** — registration, login, roles (driver, shipper, admin),
  tokens, permissions.
- **Loads** — shippers post/manage cargo to move.
- **Trips & Return Legs** — carriers post trips and their empty return legs.
- **Matching** — pairs loads with return legs by geography + time + constraints.
- **Bookings** — confirmed matches, state machine from proposed → completed.
- **Bidding / Pricing** — offers and price agreement.
- **Settlement** — records commission owed per completed booking. **MVP: the
  platform does not hold money; driver and shipper settle directly** (see
  `06_DECISION_LOG.md` ADR-0005). Collected/online payments are a future add-on.
- **Ratings & Trust** — post-trip reputation.
- **Notifications** — push/SMS/email across the lifecycle.
- **Admin / Ops** — moderation, support, dispute handling, analytics.

## 5. Data & geo strategy

- **PostgreSQL** as the system of record; **PostGIS** for spatial queries
  (points, routes, radius, lane overlap).
- **Matching** starts as **deterministic rules** (do the lanes overlap within a
  corridor and time window?) and evolves toward **ranked/ML scoring** as data
  accumulates.
- **Auditability first:** sensitive records (bookings, settlements, identity) are
  append-friendly and traceable.

## 6. API standards

- **REST + JSON** to start; schema-first via **OpenAPI**.
- **Versioned** — URL-segment `/api/v{version}/...`, default `v1`.
- **Idempotency keys** on money-moving and booking endpoints (contract reserved;
  implemented with the settlement/booking modules).
- **Filtering & sorting** standardized across list endpoints (added per module).

The concrete contract below is delivered by **M1 — API Foundation** and locked by
**ADR-0008**. Every endpoint inherits it automatically.

### 6.1 Response envelope (success *and* error — one shape)

Every response uses the same envelope, serialised camelCase:

```jsonc
// success
{ "success": true,  "message": "", "data": { /* payload */ }, "errors": [], "traceId": "…" }
// error
{ "success": false, "message": "Validation failed.", "data": null,
  "errors": [ { "field": "email", "code": "INVALID_EMAIL", "message": "Email format is invalid." } ],
  "traceId": "…" }
```

- `success` — outcome flag. `data` is populated on success and `null` on error;
  `errors` is empty on success and populated on error.
- Each error is `{ field?, code, message }` — `field` is null for non-field errors;
  `code` is a stable machine code (see `ReturnLoad.Shared.Api.ErrorCodes`).
- **Enforced globally**: controllers return bare DTOs and a result filter wraps them;
  `[SkipResponseEnvelope]` opts out for rare raw responses (file downloads, webhooks).
- Errors from *every* layer follow suit: model validation → 400, `UseStatusCodePages`
  envelopes framework 401/403/404/405, and unhandled exceptions → 500. RFC 7807
  ProblemDetails is **not** used on the wire (ADR-0008).

### 6.2 HTTP status codes

`200/201` success · `400` validation · `401` unauthenticated · `403` forbidden ·
`404` not found · `409` conflict · `500` unexpected. The `Result`/`Error` domain
type maps to these via `ResultExtensions.ToApiResult()`.

### 6.3 Request correlation (observability §10)

- `X-Correlation-ID` — accepted from the caller or generated; **echoed on the
  response** and used as the envelope `traceId` (the id a user quotes to support).
- `X-Request-ID` — unique per HTTP request.
- W3C `TraceId` (from `Activity`) — kept in logs for distributed tracing.
- All are pushed to the Serilog `LogContext`, so every log line for a request
  carries them.

### 6.4 Pagination (one shape everywhere)

List endpoints take `?page=&pageSize=` (`PaginationQuery`: page ≥ 1, pageSize
default 20, **hard cap 100**) and return `PagedResult<T>` as the `data` payload:

```jsonc
{ "page": 1, "pageSize": 20, "totalRecords": 250, "totalPages": 13, "items": [ … ] }
```

## 7. Security architecture

- **JWT / session tokens** with short lifetimes + refresh; secrets in a manager.
- **Role-Based Access Control (RBAC)**; authorization checked on every request.
  See §12 for the full role catalogue.
- **TLS everywhere**; sensitive data encrypted at rest.
- **Input validation** at the API boundary; output encoding to prevent injection.
- **Rate limiting + audit logging** on auth and settlement.
- **Dependency and secret scanning** in CI.

## 8. Environments

| Env | Purpose | Data |
|-----|---------|------|
| **Local** | Developer laptops via Docker Compose | Synthetic seed data |
| **Staging** | Prod-like integration & QA | Anonymized / synthetic |
| **Production** | Live customers | Real, protected |

- **Configuration via environment variables**; never hard-coded.
- **`.env.example`** documents every required variable; real `.env` is git-ignored.

## 9. Quality gates (CI/CD)

Every change runs through:
1. **Lint + format check**
2. **Type check / build**
3. **Automated tests** (unit + integration; e2e on critical paths)
4. **Security scan** (dependencies + secrets)
5. **Preview / staging deploy** before production

## 10. Observability

- **Structured logging** (JSON) with correlation IDs across services.
- **Metrics** on the marketplace loop (match rate, time-to-match, errors, latency).
- **Alerting** on error rates, settlement failures, and auth anomalies.

## 11. Open technical decisions (to resolve before coding)

Track and resolve these in `06_DECISION_LOG.md`:

- [x] ~~Target market / region.~~ **Resolved — ADR-0004 (India / Tamil Nadu).**
- [x] ~~Payment/settlement provider.~~ **Resolved for MVP — ADR-0005 (no held
      funds; direct settlement). A collected-payments provider becomes a new ADR
      when we add it.**
- [x] ~~Backend language & framework.~~ **Resolved — ADR-0006 (ASP.NET Core on
      .NET 9, Clean Architecture).**
- [ ] Cloud provider & deployment/orchestration target.
- [ ] Caching / realtime layer (Redis vs alternatives).
- [ ] Push notification provider(s).
- [ ] Mapping/routing provider (distance, ETAs, geocoding).
- [ ] Auth approach (self-managed vs managed identity provider).

## 12. Domain model (v1 entity map)

> We are **designing for** these entities now, not implementing them all now.
> Detailed schema + ERD is task **T-002** (`05_NEXT_TASKS.md`). Names must match
> the ubiquitous language in `02_BUSINESS_BIBLE.md` §8.

| Entity | Purpose | Key relationships |
|--------|---------|-------------------|
| **User** | Base account: identity, contact, auth, role(s). | Root of Driver / Shipper / staff accounts |
| **Driver** | A person who operates a vehicle. | belongs to a Carrier (via Association); drives Vehicles |
| **Carrier** | The company / owner-operator that owns trucks. | has many Drivers & Vehicles (via Association) |
| **Vehicle** | A truck: type, capacity, dimensions, features. | owned by Carrier; verified via Documents |
| **Load** | Cargo a Shipper needs moved. | posted by Shipper; paired via Match |
| **Trip** | A truck's planned movement incl. the Return Leg. | has a Vehicle + Driver; generates Matches |
| **TrackingEvent** | A timestamped location/status point on a Trip. | belongs to Trip/Booking (see `OFFLINE_STRATEGY.md`) |
| **Document** | Licence, RC, insurance, permit, KYC, PoD. | belongs to Driver / Vehicle / Carrier; drives verification (`08_TRUST_AND_SAFETY.md`) |
| **Payment** | A record of money owed/settled (commission). | linked to Booking; MVP = recorded, not held (ADR-0005) |
| **Invoice** | Commission / billing document. | issued against Bookings/Payments |
| **Dispute** | A raised conflict on a booking/load. | belongs to Booking; handled by Ops/Support |
| **Notification** | Push/SMS/email sent across the lifecycle. | targets Users; triggered by events |
| **Rating** | Post-trip trust signal each side leaves. | between parties on a completed Booking |
| **Association** | Links a User/Driver to a Carrier with a role. | join between User/Driver and Carrier |
| **AuditLog** | Immutable who/when/what for sensitive actions. | references any auditable entity (append-only) |

> **Match** and **Booking** (from the glossary) sit alongside these as the pairing
> and commitment records; the matching rules are specified in `MATCHING_ENGINE.md`.

## 13. Roles & access model (RBAC)

Authorization is enforced per request against these roles. Being authenticated is
never the same as being authorized (`01_PROJECT_RULES.md` §5).

| Role | Belongs to | Can (high level) |
|------|-----------|------------------|
| **Platform Admin** | Internal | Full control; policy, blacklist, role management |
| **Operations** | Internal | Verify documents, moderate, triage fraud, manage disputes |
| **Finance** | Internal | Commission records, invoices, reconciliation, reporting |
| **Support** | Internal | Assist users, view (not alter) records, escalate |
| **Carrier Owner** | Carrier | Manage own carrier's Drivers, Vehicles, documents |
| **Dispatcher** | Carrier | Assign drivers/trucks to trips, manage return legs |
| **Driver** | Carrier | Own trips, status updates, tracking, documents |
| **Shipper** | External | Post/manage Loads, accept Matches, book |
| **Association Manager** | Carrier/Group | Manage Associations (who belongs to which carrier) |

Internal roles (Admin/Ops/Finance/Support) are separated so that, e.g., Support
cannot blacklist and Finance cannot alter verification — **least privilege**
(`01_PROJECT_RULES.md` §5).

---

*This document is expected to grow. Keep it accurate — an out-of-date technical
bible is worse than none.*
