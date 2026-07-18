# 06 — DECISION LOG

> An append-only journal of **every significant decision** and *why* we made it.
> This is our institutional memory. Future-us (and future agents) will thank us.
>
> Format is a lightweight **ADR (Architecture Decision Record)**. Newest at top.
> Never rewrite history — if a decision changes, add a **new** entry that
> supersedes the old one and mark the old one `Superseded`.

---

## Entry template (copy for each new decision)

```
### ADR-XXXX — <short title>
- **Date:** YYYY-MM-DD
- **Status:** Proposed | Accepted | Superseded by ADR-YYYY | Rejected
- **Context:** What problem/decision are we facing? What constraints apply?
- **Decision:** What did we choose?
- **Alternatives considered:** What else, and why not?
- **Consequences:** Trade-offs, follow-ups, and what this locks in or unlocks.
```

---

## ADR-0016 — MVP sprint (M4): application services, clients, GPS/payments placeholders
- **Date:** 2026-07-18
- **Status:** Accepted
- **Context:** The MVP sprint builds runnable end-to-end flows (onboarding → verification →
  load → trip) across backend, Angular admin, and Flutter mobile, on the M3.5 model.
- **Decision:**
  1. **Application-service use-cases** (not MediatR/CQRS): plain interfaces + internal
     implementations in `Application/UseCases/*`, over `IRepository<T>`/`IUnitOfWork`,
     returning `Result`. Controllers stay thin; the M1 envelope + M1.5 hardening + M2
     policy authorization are preserved. `DomainException` now maps to HTTP 400.
  2. **Angular admin** (standalone, zoneless, Material): JWT `AuthService` + functional
     HTTP interceptor + `authGuard`; envelope-unwrapping `ApiService`; login + shell +
     drivers/documents(approve)/loads/trips/settings. Verified via `ng build`.
  3. **Flutter mobile** (Riverpod + GoRouter + Dio + secure storage): login → dashboard →
     loads(accept) → tracking, token via secure storage + Dio interceptor. **Not
     build-verified — the Flutter SDK is not installed in this environment.**
  4. **GPS:** no Maps API key configured, so the tracking screen shows the real recorded
     tracking points + a clear "Maps integration pending API key" banner — **no fake live
     map** is rendered. A map widget replaces the banner when a key is supplied.
  5. **Payments:** `IPaymentService` **interfaces only** (ADR-0005 reaffirmed — the
     platform records commission, does not hold funds); no gateway.
  6. **Dev seeding:** Development startup migrates + seeds demo users/roles/carrier/shipper
     profile (resilient if the DB is down). A dev-only AES key enables Aadhaar-at-rest.
- **Consequences:** Backend + admin are build- and test-verified (147 backend tests; ng
  build green). The mobile app is written to the same contracts but unverified pending a
  Flutter SDK. Live PostgreSQL run + a browser/device click-through remain a manual step
  (Docker is available; instructions provided). List/query endpoints exist only where the
  console needs them (drivers, pending documents, available loads); broader querying grows
  per feature. Aadhaar/email/etc. remain governed by earlier ADRs.

## ADR-0015 — Persistence foundation (M3.5): EF Core mapping, encryption, concurrency
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** M3.5 persists every M3 aggregate with EF Core + Npgsql (no business logic,
  APIs, or UI), keeping the domain persistence-ignorant (ADR-0006/0014).
- **Decision:**
  1. **Mapping in Infrastructure:** one `IEntityTypeConfiguration<T>` per aggregate;
     single-value value objects via **value converters** (indexable columns), multi-field
     VOs via **owned types** (`OwnsOne`, incl. nested `ReturnLeg`); enums stored as **text**.
     EF materialises through private parameterless constructors added to aggregates/VOs
     (the only domain change — behaviour is unaffected; factories remain the creation path).
  2. **Cross-cutting via shadow properties + a SaveChanges interceptor** (no domain
     pollution): soft delete (`IsDeleted` + global query filter; deletes become updates),
     audit (`CreatedBy`/`UpdatedBy`/`UpdatedAtUtc`; created-at is a domain property), and
     an **application-managed `Version` concurrency token** — chosen over SQL Server
     `rowversion`/Npgsql `xmin` because it is **provider-portable** (works on Postgres and
     the SQLite test DB) and testable.
  3. **Encryption at rest:** Aadhaar is stored via an AES-GCM `IFieldEncryptor` value
     converter (Trust & Safety §1; 01_PROJECT_RULES.md §5/§6); the key comes from the
     secret store (`Encryption:Key`), never committed. One `Documents` table serves all
     owners via `OwnerType` (unifies "DriverDocument"/"VehicleDocument"). `VehicleType` is
     an enum column, not a lookup table (it is a domain enum, ADR-0014).
  4. **Repositories:** generic `IRepository<T>` + `IUnitOfWork` in Application, EF
     implementations in Infrastructure (aggregate-specific queries arrive per feature
     milestone). Initial business **migration** `M3_5_Persistence` (13 tables, FKs,
     unique/normal/composite indexes) with a verified symmetric `Down`.
  5. **Tests on SQLite in-memory** for genuine relational enforcement (unique indexes, FKs,
     concurrency) that EF InMemory cannot provide. The SQLite native lib's advisory
     (GHSA-2m69-gcr7-jv3q) is **suppressed for the test project only** — not shipped;
     production is PostgreSQL — reversing the M1.5/M2 avoidance with this narrow, documented
     justification.
- **Alternatives considered:** JSON columns for all VOs (rejected: can't index pickup/drop
  or unique fields); `xmin` concurrency (rejected: Npgsql-only, untestable here); splitting
  Document into two tables (rejected: diverges from the single Document aggregate); EF
  InMemory for tests (rejected: no constraint/concurrency enforcement).
- **Consequences:** The domain is fully persistable with security and integrity built in.
  Live apply/rollback + a real-Postgres integration run are deferred to the local Docker
  env (T-010) — the migration SQL is generated and the mapping is proven on SQLite. Geo
  pickup/drop indexing will move to PostGIS (spatial) in the geo/matching milestone.

## ADR-0014 — Core domain model (M3): DDD tactical patterns, guard-clause invariants
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** M3 builds the business domain model (the work `03_TECHNICAL_BIBLE.md` §12
  reserved as T-002) across all bounded contexts, in the Domain layer only — no APIs,
  services, EF configs, migrations, or business logic beyond the model itself.
- **Decision:**
  1. **Tactical DDD patterns** in `ReturnLoad.Domain`: `AggregateRoot<TId>` (records
     `IDomainEvent`s), `ValueObject` (value equality), `BaseEntity<TId>`. Aggregates are
     `sealed`, constructed via static factories, and mutated only through intention-named
     methods that keep invariants true.
  2. **Invariants enforced by guard clauses that throw `DomainException`** (with a stable
     `Code`), *not* the `Result` pattern. Rationale: the Domain depends on **nothing**
     (ADR-0006) so it cannot use `Shared.Result`; an invalid entity must be
     unconstructable, and throwing keeps constructors/factories terse and rules testable.
     The application layer maps `DomainException.Code` to the API envelope (ADR-0008).
  3. **Bounded contexts as namespaces/folders** in one Domain project (modular monolith,
     ADR-0002, ADR-0011): Identity, Fleet, Documents, Loads, Trips, Tracking, Reviews,
     Administration. Cross-context references use ids (Guid), not navigation, to keep
     aggregates independent.
  4. **India-localised value objects** (ADR-0004): `MobileNumber`, `Money` (INR),
     `Distance` (km), `AadhaarNumber` (masked, PII-sensitive), `DrivingLicenceNumber`,
     `VehicleRegistrationNumber`, `GstNumber`, plus `EmailAddress`, `Weight`,
     `GeoCoordinate`, `Location`, `TimeWindow`.
  5. **Cross-aggregate rules stay at the edge:** e.g. `Vehicle.Activate(bool
     mandatoryDocumentsValid)` and `DriverProfile.MarkVerified()` express the rule but the
     document-validity check is supplied by the application layer (matching filters 7–8,
     Trust & Safety). Verification is modelled as a **state with expiry** on `Document`
     ("fail closed"). `AuditLog` and `TrackingEvent` are append-only; TrackingEvent keeps
     the device's truthful captured-at time (OFFLINE_STRATEGY §7).
- **Alternatives considered:** `Result`-returning factories (rejected: Domain can't
  reference Shared and exceptions model "impossible state" better); anemic
  entities + separate services (rejected: violates encapsulation — invariants would leak);
  a project-per-context split (rejected now: premature; namespaces suffice for a modular
  monolith and a split can follow context lines later).
- **Consequences:** The whole domain vocabulary exists and is unit-tested (invariants,
  value objects, events). **Milestone reshape:** M3 = *Core Domain Model* (all contexts),
  superseding the earlier "M3 = User" line in ADR-0009; subsequent milestones add the
  application/API/persistence layers **per context** on top of this model. No persistence
  yet — EF configurations + migrations for these entities are the next milestone.

## ADR-0013 — Authentication foundation (M2): self-managed identity + own JWTs
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** M2 needs authentication before any business feature. The approach was the
  last open item in `03_TECHNICAL_BIBLE.md` §11. Design reviewed in full at
  `docs/design/M2_AUTHENTICATION_DESIGN_REVIEW.md` and approved by the co-founder with the
  decisions below. Clients are the Flutter mobile app and the Angular admin SPA.
- **Decision:**
  1. **Framework:** **ASP.NET Core Identity** for the user store + credential primitives
     (hashing, lockout, security stamps, token providers), backed by EF Core — but the API
     **issues its own JWTs** (no Identity cookies/UI). Self-managed, not a managed IdP.
     `User` stays lean (auth only); Driver/Shipper/staff are separate profiles keyed by
     `UserId`.
  2. **Refresh-token transport:** **response body** (uniform across Flutter secure storage
     and the Angular SPA; keeps the API stateless).
  3. **Password hasher:** **PBKDF2** (Identity default). **Argon2id is a future migration
     option** behind `IPasswordHasher<T>` if requirements change.
  4. **Token signing:** **HS256 for MVP → RS256 + JWKS for production.** The signer is
     **abstracted behind an interface** (`ITokenSigner`) so switching algorithms needs no
     business-logic change; support multiple keys via `kid` for zero-downtime rotation.
  5. **Lifetimes & lockout:** access **15 min**, refresh **7 days** (a "Remember Me"
     extension is a future option). Lockout: **5 failed attempts → 15-min lock**; the
     failure count **resets on successful login**; **every failed attempt is audited**;
     IP/device rate limiting reuses the M1.5 `sensitive` policy.
  6. **Sessions:** **multi-device** (Option A) — independent rotating refresh-token
     families per device; logout revokes one, logout-all revokes all + bumps the security
     stamp / `permissionsVersion`.
  7. **Token claims:** `sub`, `userId`, `role`(s), `permissionsVersion`, `tenantId`
     (reserved), `deviceId` (optional), `jti`, `iat`, `exp`. **`permissionsVersion`**
     lets a role/permission change invalidate outstanding access tokens by version
     comparison — no token-format redesign.
- **Alternatives considered:** fully custom identity (rejected: re-implements crypto);
  managed IdP such as Auth0 (rejected for MVP: cost, external dependency, India
  data-residency questions — revisit for enterprise SSO); HttpOnly-cookie refresh
  (rejected: complicates the mobile client; response-body is uniform).
- **Consequences:** Resolves §11 "auth approach". RBAC uses the 9 roles (§13) via
  **policy-based** authorization with a permission-catalogue seam for future fine-grained
  permissions. **M2 scope explicitly excludes SMS OTP, email verification, and document
  verification** — these plug into this foundation in later milestones (Notifications,
  M6 Trust & Safety) rather than complicating the core now. Authentication (this ADR) is
  kept separate from **trust verification / transactability** (M6) per the design review §11.

## ADR-0012 — File storage abstraction, interfaces-first
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** The platform will handle sensitive documents (RC, insurance, licence,
  permits — the Documents context, M6). The storage backend (local disk / Azure Blob
  / AWS S3 / MinIO) depends on the still-open cloud decision (`03_TECHNICAL_BIBLE.md`
  §11). Business code must never couple to a concrete provider.
- **Decision:** Introduce **`IFileStorageService` now, interfaces only** —
  `SaveAsync`, `GetAsync`, `DeleteAsync`, `GenerateTemporaryUrl` (signed, time-limited
  access). A local-disk implementation is acceptable for development; the production
  backend is chosen later and swapped **without touching business code**. Actual
  document upload lands in **M6**, not M2.
- **Alternatives considered:** Wait until M6 (rejected: risks business code coupling
  to a provider before the abstraction exists); pick a cloud provider now (rejected:
  cloud is an open §11 decision — see ADR-0003).
- **Consequences:** Document handling plugs into a stable seam; a provider swap is an
  infrastructure-only change. Concrete placement (which project/context owns the
  implementation) is decided when it is implemented.

## ADR-0011 — Adopt bounded contexts for module organisation
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** As modules multiply (users, drivers, vehicles, documents, loads,
  matching…), we need an explicit modularity discipline to keep the modular monolith
  (ADR-0002) from decaying into a big ball of mud.
- **Decision:** Organise the domain into **bounded contexts**: **Identity, Fleet,
  Loads, Trips, Tracking, Documents, Billing, Notifications, Administration**. Every
  module belongs to exactly one context. Milestone → context map: M2 Auth + M3 User →
  **Identity**; M4 Driver + M5 Vehicle → **Fleet**; M6 → **Documents**; M7 GPS →
  **Tracking**; M8 → **Loads**; M9 Matching → **Matching/Trips**; settlement →
  **Billing**; notifications → **Notifications**; admin console → **Administration**.
- **Alternatives considered:** Technical-layer-only organisation (rejected: does not
  scale as the domain grows); microservices now (rejected: premature — stay a modular
  monolith first, ADR-0002).
- **Consequences:** Clear ownership and boundaries; a future service split, if ever
  needed, can follow context lines cheaply. Physical structure (folders / namespaces /
  projects) is decided per module as it lands.

## ADR-0010 — Security Foundation (M1.5) precedes Authentication (M2)
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** M2 will expose the first real endpoints (authentication). Authentication
  answers *who the user is*; it does not answer *how the application is protected*.
  Opening that door before the platform is hardened would mean auth endpoints exist
  without the protections around them.
- **Decision:** Insert milestone **M1.5 — Security Foundation** between M1 and M2.
  Scope is platform hardening only — **no business logic, no authentication
  endpoints**: security response headers (HSTS, X-Content-Type-Options, frame
  protection, Referrer-Policy, a baseline CSP for the API), HTTPS redirection policy,
  a config-driven **CORS** allowlist, **rate limiting** (ASP.NET Core rate limiter),
  request body **size limits**, file-upload size / **MIME allowlist** (limits only —
  storage is ADR-0012), **JWT bearer configuration** bound from config (validation
  parameters only — *no* login or token issuance), a **secret-management strategy**
  (env vars now per §8; external manager deferred to the cloud decision, §11),
  **password-policy configuration** (values only), **security event logging**, and
  basic **API abuse protection**. Delivered with tests and obeying the M1 envelope +
  correlation contract (ADR-0008).
- **Alternatives considered:** Go straight to M2 (rejected: auth endpoints would ship
  before platform protections); fold hardening into M2 (rejected: mixes concerns and
  bloats the authentication milestone).
- **Consequences:** M2 is built on an already-hardened platform. Some values are
  placeholders pending the open cloud / secret-manager decision (§11). No business
  scope is introduced. **M2 (Authentication) additionally requires an approved
  Authentication Design Review before any code** — design first, implementation second.

## ADR-0009 — Adopt milestone naming (M0–M9) for delivery
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** The backlog used fine-grained task IDs (`T-0xx`). For communicating
  progress to future collaborators and investors, a coarser, product-shaped
  vocabulary reads more naturally and makes "where are we?" answerable in one line.
- **Decision:** Track delivery as **milestones**: **M0** Bootstrap, **M1** API
  Foundation, **M2** Authentication, **M3** User, **M4** Driver, **M5** Vehicle,
  **M6** Documents, **M7** GPS, **M8** Loads, **M9** Matching. The detailed `T-0xx`
  backlog in `05_NEXT_TASKS.md` remains as the execution-level breakdown beneath the
  milestones. **M0 = done** (bootstrap, ADR-0006); **M1 = done** (this ADR-0008);
  **M2 Authentication = next.**
- **Alternatives considered:** Keep only `T-0xx` ids (rejected: not communication-
  friendly for non-engineers); replace `T-0xx` entirely (rejected: loses useful
  task-level granularity we already rely on).
- **Consequences:** One shared roadmap vocabulary. Milestones map to `T-0xx` items,
  so nothing is lost. Authentication (M2) was deliberately deferred behind M1 so its
  endpoints inherit the API contract from day one.

## ADR-0008 — Unified response envelope for the entire API (supersedes ProblemDetails)
- **Date:** 2026-07-12
- **Status:** Accepted
- **Context:** `03_TECHNICAL_BIBLE.md` §6 called for "consistent envelopes for
  success **and** error", but the T-001 scaffolding returned a success envelope for
  200s and **RFC 7807 ProblemDetails** for errors — two shapes. Before the first
  business feature (M2 Authentication) exposes any endpoint, the API needs one
  contract every future endpoint inherits automatically. This is milestone **M1**.
- **Decision:** **Every** response — success and error, including framework-level
  400/401/403/404/405/500 — uses one envelope:
  `{ success, message, data, errors[], traceId }`, where each error is
  `{ field?, code, message }`. Enforced by **automatic global wrapping** (an MVC
  result filter wraps any bare controller return; an opt-out attribute exists for
  rare raw responses). The model-validation 400, the status-code handler
  (`UseStatusCodePages`), and the exception handler all emit the same envelope.
  **Request correlation** (`X-Correlation-ID` in/out, `X-Request-ID`, W3C trace id,
  Serilog-enriched) populates `traceId`. **Pagination** is one shape everywhere
  (`page, pageSize, totalRecords, totalPages, items`, capped at 100/page).
  **Versioning** stays URL-segment `/api/v1`. This **supersedes** the ProblemDetails
  approach on the wire; `AddProblemDetails()` remains registered only as the inert
  framework fallback and is never emitted.
- **Alternatives considered:** Keep RFC 7807 ProblemDetails for errors (rejected:
  two shapes, contradicts the "one shape" goal and complicates every mobile/admin
  client); envelope-with-ProblemDetails via content negotiation (rejected: extra
  machinery for a hypothetical third-party consumer we do not have); explicit
  per-endpoint wrapping helpers (rejected: relies on discipline; a global filter
  makes forgetting impossible).
- **Consequences:** One deserialisation path for all clients; a failure is never
  structurally different from a success; every response is traceable by `traceId`.
  Trade-off: we give up RFC 7807 interop with generic API tooling — acceptable for a
  first-party mobile/admin API, and revisitable via a new ADR if we later publish a
  public/partner API. No new NuGet dependencies were added. Contract detailed in
  `03_TECHNICAL_BIBLE.md` §6.

## ADR-0007 — Defer AutoMapper from the foundation (security + licensing)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** The bootstrap stack named AutoMapper for object mapping. During the
  T-001 build, the CI dependency audit (`NuGetAudit`, treated as an error per
  `01_PROJECT_RULES.md` §5) flagged the free AutoMapper line with a **high-severity
  DoS advisory (CVE-2026-32933 / GHSA-rvv3-g6hj-g44x)**. The patched releases
  (>= 15.1.1) are under AutoMapper's **new commercial licence**. The foundation has
  **zero mappings**, so nothing depends on it yet.
- **Decision:** Do **not** reference AutoMapper in the foundation. Introduce an
  object-mapping approach when the first real mapping is needed, choosing then
  between a vetted free library (e.g. Mapster), explicit hand-mapping, or a
  licensed AutoMapper if the business approves the cost. The dependency audit gate
  stays **on**.
- **Alternatives considered:** Pin the vulnerable free AutoMapper 13 (rejected:
  ships a known high-severity vuln to `main`); adopt commercial AutoMapper 15+
  now (rejected: unapproved licence cost for a dependency with no current usage);
  suppress the audit warning (rejected: defeats the security gate).
- **Consequences:** `main` stays free of known-vulnerable dependencies. A small
  future decision (which mapper) is deferred to when it is actually needed, with
  no code to migrate because there are no mappings today. FluentValidation (MIT,
  clean) remains wired in the Application layer.

## ADR-0006 — Backend framework: ASP.NET Core on .NET (Clean Architecture)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** `03_TECHNICAL_BIBLE.md` §11 listed "backend language & framework" as
  an **open** decision (ADR-0003 proposed Node/NestJS, Java/Spring, or Go as
  candidates). The bootstrap task (T-001) locked the choice as **.NET / ASP.NET
  Core**. The brief said ".NET 8", but the only SDK available in the build
  environment is **.NET SDK 9**; targeting net9.0 is fully build- and
  run-verifiable there, whereas net8.0 could only be built, not run.
- **Decision:** The backend is **ASP.NET Core Web API** structured as **Clean
  Architecture** — projects `ReturnLoad.Api` (composition root), `.Application`,
  `.Domain`, `.Infrastructure`, `.Shared`, plus `UnitTests` / `IntegrationTests`.
  Dependencies point inward (Api → Application/Infrastructure → Domain; Shared is
  cross-cutting; Domain depends on nothing). Target framework **net9.0**.
  Persistence is **EF Core + Npgsql (PostgreSQL/PostGIS)**; logging **Serilog**;
  validation **FluentValidation**; docs **Swagger/OpenAPI**; **URL-segment API
  versioning** (`/api/v1`); **JWT bearer** wired as framework-only; **SignalR**
  as a realtime foundation; containerised via a multi-stage **Dockerfile**.
- **Alternatives considered:** Node.js/NestJS, Java/Spring, Go (all viable; .NET
  chosen by the co-founder for team fit and a batteries-included, strongly-typed
  platform). **.NET 8 vs .NET 9:** net9.0 chosen because it is the installed,
  verifiable SDK; revisit if a project constraint requires LTS (.NET 8/10).
- **Consequences:** Resolves the largest open item in §11. Establishes the layer
  boundaries and cross-cutting standards every future backend task inherits. If an
  LTS runtime is later mandated, the multi-targeting change is a small, isolated
  edit to `Directory.Build.props`. Business modules, entities, migrations, and
  auth logic remain **out of scope** until their own tasks (T-002+).

## ADR-0005 — MVP settlement is direct (platform does not hold money)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** Holding customer funds in India (escrow / pooled accounts) triggers
  significant regulatory obligations (payment-aggregator licensing, RBI norms,
  KYC/AML). We do not want early regulatory complexity to block shipping the core
  marketplace value: matching empty return legs to loads.
- **Decision:** For the MVP, **the platform does not hold or move money.** Driver
  and shipper **settle directly** between themselves. The platform only **records
  the commission owed** to it per completed booking. Online/collected payments are
  a **future** capability, not part of the MVP.
- **Alternatives considered:** Integrating a payment aggregator / escrow from day
  one (rejected for now: regulatory + compliance load, licensing lead time, slows
  the core loop); acting as merchant of record (rejected: even heavier).
- **Consequences:** Fast path to launch with minimal regulatory surface. We accept
  that commission collection is initially a **recorded receivable**, not an
  automated capture, and that reconciliation/settlement UX is manual at first.
  When we add collected payments, that becomes a **new ADR** and revisits
  `03_TECHNICAL_BIBLE.md` §11. Business framing lives in `02_BUSINESS_BIBLE.md` §4.

## ADR-0004 — Lock the initial target market: India / Tamil Nadu
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** The knowledge base carried a latent contradiction — US framing
  ("deadhead miles") alongside an India example (Chennai→Bangalore). Region is a
  root decision: it fixes currency, units, language, tax, payment regulation, and
  compliance, and every future module inherits it. It cannot stay ambiguous.
- **Decision:** The initial target market is:
  - **Country:** India
  - **State:** Tamil Nadu
  - **Initial routes:** within Tamil Nadu (intra-state)
  - **Currency:** INR (₹)
  - **Distance unit:** kilometres (km)
  - **Languages:** Tamil & English
  - **Compliance regime:** Indian regulations (see `08_TRUST_AND_SAFETY.md`)
- **Alternatives considered:** A market-agnostic build (rejected: forces us to
  abstract currency/units/compliance prematurely and delays real value); a broader
  pan-India launch on day one (rejected: dilutes liquidity — a marketplace needs
  density in one region first).
- **Consequences:** Removes ambiguity from every module. Units are km, money is
  INR, UI is Tamil + English, and Trust & Safety uses Indian sources (VAHAN,
  SARATHI/Parivahan, Aadhaar, DigiLocker, GST, permits). Design should remain
  *localisable* so expanding to other states/countries is a configuration effort,
  not a rewrite. `02_BUSINESS_BIBLE.md` is updated to km/INR to match.

## ADR-0001 — Adopt an AI-first knowledge base to govern the project
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** This is a production-grade, long-lived platform built with heavy AI
  agent involvement. We need a single, durable source of truth so any contributor —
  human or AI — starts from the same rules, business context, and technical
  direction.
- **Decision:** Create an `/ai` knowledge base (`00`–`06`) that every session reads
  first: master prompt, project rules, business bible, technical bible, current
  task, next tasks, and this decision log.
- **Alternatives considered:** Scattering context across READMEs and wiki pages
  (rejected: drifts out of sync, no clear entry point); relying on chat history
  (rejected: ephemeral, not versioned).
- **Consequences:** Adds a small documentation discipline. In return we get
  consistent, auditable, resumable work and far less repeated context-setting.

## ADR-0002 — Monorepo with dedicated top-level folders per concern
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** We have multiple deliverables (backend, mobile, admin, database,
  infra) that must evolve together and share a domain language.
- **Decision:** Use a single monorepo with clear top-level folders:
  `/ai /backend /mobile /admin /database /docs /docker /scripts`.
- **Alternatives considered:** Multiple separate repositories (rejected for now:
  higher coordination cost, harder atomic cross-cutting changes early on).
- **Consequences:** Simpler cross-cutting changes and shared tooling now; we accept
  we may need to enforce module boundaries and, later, split repos if scale demands.

## ADR-0003 — Proposed baseline stack (NOT yet locked)
- **Date:** 2026-07-11
- **Status:** Proposed
- **Context:** We need a starting direction, but several choices deserve deliberate
  evaluation before we commit code to them.
- **Decision (proposed):** Flutter for `/mobile`, Angular for `/admin`,
  PostgreSQL (+PostGIS) for `/database`, Docker for local environments. Backend
  framework, cloud provider, caching/realtime layer, payments, notifications,
  mapping, and auth approach remain **open**.
- **Alternatives considered:** To be documented per-choice when each is resolved
  (see `03_TECHNICAL_BIBLE.md` §11).
- **Consequences:** Gives us a concrete direction to react to without prematurely
  locking hard-to-reverse choices. Each open item becomes its own ADR once decided.

---

*Discipline: no significant decision is "made" until it appears here as
**Accepted**. If it's not written down, it can be relitigated forever.*
