# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M3.5 — Persistence Foundation** — ✅ **COMPLETE, awaiting review.**
(Decisions in **ADR-0015**. Prior: M0–M3 ✅; M3 domain model = ADR-0014.)

### Goal
Persist every M3 aggregate with EF Core + PostgreSQL — mapping, security, integrity —
with **no business logic, APIs, or UI**.

### Delivered
- **EF configurations** for all 13 aggregates (Infrastructure): value converters for
  single-value VOs, owned types for multi-field VOs (incl. nested `ReturnLeg`), enums as
  text.
- **Security:** Aadhaar **encrypted at rest** (AES-GCM `IFieldEncryptor`; key from secret
  store). Sensitive file identifiers reference storage keys only (ADR-0012).
- **Integrity conventions** via shadow props + SaveChanges interceptor: **soft delete**
  (+ global query filter), **audit** fields, **app-managed `Version` concurrency token**.
- **Indexes:** unique (Mobile, Email, Registration, Licence, AuthUserId); normal (Driver/
  Vehicle/Load/Trip status); composite (Tracking `TripId+CapturedAtUtc`, `Driver+Vehicle`,
  Load `Shipper+Status`, Document `Owner`, etc.). **FKs** on every aggregate reference.
- **Repositories:** `IRepository<T>` + `IUnitOfWork` (Application) with EF implementations
  (Infrastructure).
- **Migration** `M3_5_Persistence` — 13 tables, symmetric `Down`; SQL script generates.
- **Tests:** **146 green** (110 unit, 26 integration incl. 8 SQLite persistence tests —
  mapping/VO/enum/encryption/uniqueness/FK/concurrency/soft-delete — 10 architecture).

### NOT built (by instruction)
Driver/Vehicle/Load APIs, services, GPS, matching, payments, notifications delivery,
Flutter, Angular.

### Known follow-ups (deferred, honest)
- Live PostgreSQL apply/rollback + a real-DB integration run → local Docker env (T-010);
  the migration SQL is generated and mapping is proven on SQLite.
- Geo pickup/drop indexing → PostGIS spatial indexes in the geo/matching milestone.

### Status
**AWAITING CO-FOUNDER REVIEW.** Recommended next: **M4 — Identity & Onboarding APIs**
(application/API layer for Carrier/Driver/UserProfile on the now-persisted model).

---

*When approved, move this to the log and promote the next item.*
