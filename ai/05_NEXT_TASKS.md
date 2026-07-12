# 05 — NEXT TASKS

> The prioritized backlog. The top item becomes `04_CURRENT_TASK.md` when we start
> it. Keep this ordered by *what unblocks the most value soonest*.

---

## Milestone map (M0–M9) — the product-shaped roadmap (ADR-0009)

Coarse, communication-friendly milestones, each owned by one **bounded context**
(ADR-0011). The detailed `T-0xx` items below are the execution breakdown beneath them.

| Milestone | Scope | Context | Status |
|-----------|-------|---------|--------|
| **M0** | Bootstrap — stack locked, three scaffolds, tests/CI harness | Platform | ✅ Done (ADR-0006) |
| **M1** | **API Foundation** — response envelope, error/validation contract, correlation ids, versioning, pagination | Platform | ✅ Done (ADR-0008) |
| **M1.5** | **Security Foundation** — headers, HTTPS, CORS, rate limiting, size/upload/MIME limits, JWT config (no login), secret strategy, password policy, security logging, abuse protection | Platform | ⏭️ **Next** (ADR-0010) |
| **M2** | **Authentication** — identity, JWT, refresh tokens, roles, permissions, authorization policies. **No document uploads.** Requires an approved Authentication Design Review first. | Identity | ⬜ (ADR-0010) |
| **M3** | User | Identity | ⬜ |
| **M4** | Driver | Fleet | ⬜ |
| **M5** | Vehicle | Fleet | ⬜ |
| **M6** | Documents (KYC / RC / insurance / licence / permit) — uses `IFileStorageService` (ADR-0012) | Documents | ⬜ |
| **M7** | GPS / location | Tracking | ⬜ |
| **M8** | Loads | Loads | ⬜ |
| **M9** | Matching | Matching / Trips | ⬜ |

> **Sequencing rationale:** hardening (M1.5) before authentication (M2) means every
> auth endpoint is born behind the platform's protections. `IFileStorageService`
> (interfaces only, ADR-0012) is introduced early so the Documents context (M6) plugs
> into a stable seam — no document *uploads* before M6.
>
> **Bounded contexts** (ADR-0011): Identity · Fleet · Loads · Trips · Tracking ·
> Documents · Billing · Notifications · Administration. Every module belongs to one.

---

## Phase 0 — Decisions (do before coding)

- [~] **T-001 — Lock the stack + bootstrap the foundation.** ✅ Backend framework
      locked (ADR-0006, ASP.NET Core/.NET 9) and all three stacks scaffolded (see
      `04_CURRENT_TASK.md`). **Still open** in `03_TECHNICAL_BIBLE.md` §11: cloud,
      cache, notifications, maps, auth provider — decide each as its module needs it.
      _Resolved already: target market (ADR-0004), MVP payments (ADR-0005)._
- [ ] **T-002 — Define the domain model (v1).** Entities and relationships for
      Carrier, Driver, Shipper, Load, Trip, Return Leg, Match, Booking, Bid,
      Settlement, Rating. ERD in `/database` + `/docs`.
- [~] **T-003 — API contract (v1).** ✅ **Foundation delivered by M1** (ADR-0008):
      response envelope, error/validation contract, correlation ids, pagination,
      versioning — the shape every endpoint inherits. **Remaining:** the per-module
      OpenAPI *schemas* (auth, loads, trips, matching, bookings), added with each
      module.

## Phase 1 — Foundational engineering

- [ ] **T-010 — Local dev environment.** `docker-compose` with PostgreSQL/PostGIS
      (and cache once chosen). One command to bring the stack up locally.
- [~] **T-011 — Backend skeleton.** ✅ Mostly delivered by T-001 (scaffold, env
      config, health endpoint, Serilog, xUnit harness, analyzers/format). **Remaining:
      the CI pipeline** (lint + build + test + dependency/secret scan on every push).
- [ ] **T-012 — Database migrations (v1).** Implement the T-002 model as versioned
      migrations + seed data.
- [ ] **T-012.5 — Security Foundation (= milestone M1.5, next).** Platform hardening
      per ADR-0010: security headers, HTTPS redirection, CORS allowlist, rate
      limiting, request/upload/MIME limits, JWT config (validation only, no login),
      secret strategy, password-policy config, security logging, abuse protection.
      Plus `IFileStorageService` interfaces (ADR-0012). No business logic.
- [ ] **T-013 — Identity & Access (= milestone M2).** Identity, JWT, refresh tokens,
      roles, permissions, authorization policies (`03_TECHNICAL_BIBLE.md` §13). **No
      document uploads.** Every endpoint uses the M1 contract (ADR-0008) on the M1.5-
      hardened platform. **Gate:** an approved *Authentication Design Review* precedes
      any code (identity model, token lifecycle, refresh rotation, role/permission
      strategy, account states, password reset, email/OTP, sessions, audit events,
      future external IdPs).
- [ ] **T-014 — Trust & Safety (verification).** Implement the pre-trip gate from
      `08_TRUST_AND_SAFETY.md`: Document entity, driver KYC, RC / insurance /
      licence / permit verification states, expiry reminders, fraud reports,
      blacklist, audit trail. Blocks matching (a party must be verified to match).

## Phase 2 — Core marketplace

- [ ] **T-020 — Loads module.** Shippers create/manage loads.
- [ ] **T-021 — Trips & Return Legs module.** Carriers post trips + empty legs.
- [ ] **T-022 — Matching engine (v1, rules-based).** Lane + time-window overlap
      → ranked candidate matches. Highest test coverage.
- [ ] **T-023 — Bookings state machine.** proposed → accepted → in-transit →
      completed → settled, with guards and audit trail.
- [ ] **T-024 — Bidding & pricing.** Offers and price agreement on a match.

## Phase 3 — Clients

- [ ] **T-030 — Mobile app skeleton (Flutter).** Auth, map, post/return-leg flow.
- [ ] **T-031 — Admin console skeleton (Angular).** Ops dashboard, moderation,
      support views.

## Phase 4 — Money & trust

- [ ] **T-040 — Settlement.** Integrate payment provider; payouts + platform fee;
      idempotent, audited.
- [ ] **T-041 — Ratings & trust.** Post-trip reputation for both sides.
- [ ] **T-042 — Notifications.** Push/SMS/email across the booking lifecycle.

## Phase 5 — Operate

- [ ] **T-050 — Observability.** Metrics for the marketplace loop, dashboards,
      alerting.
- [ ] **T-051 — Staging + production deploy pipelines.**

---

*Rules for this file: keep it prioritized, keep items small enough to finish, and
promote exactly one item at a time into `04_CURRENT_TASK.md`.*
