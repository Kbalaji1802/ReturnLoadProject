# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M1.5 — Security Foundation (+ File Storage Abstraction)** — ✅ **COMPLETE, awaiting review.**
(ADR-0010, ADR-0012. Prior: M0 Bootstrap ✅, M1 API Foundation ✅ / ADR-0008.)

### Goal
Harden the platform *before* authentication (M2) exposes endpoints, so every future
endpoint is born behind the protections. Platform hardening only — **no business
logic, no auth endpoints, no token issuance, no document uploads.**

### What was delivered
- **Security headers** on every response (nosniff, frame-DENY, referrer, CSP; `Server`
  suppressed; CSP relaxed only for Swagger in Dev).
- **HTTPS/HSTS** (config-gated, outside Development) + forwarded-headers for proxies.
- **CORS** config-driven allowlist (empty = none); correlation headers exposed.
- **Rate limiting** (per-IP fixed window + reserved `sensitive` policy) → **429 envelope**
  + security-event log.
- **Request body limit** (Kestrel, 10 MB) and **file upload** size/MIME/extension
  allowlists via `FileUploadValidator`.
- **JWT config** bound + **fail-fast** validated (no issuance yet); **password policy**
  values; **security event logging** on 401/403/429.
- **`IFileStorageService`** (Application) + **local-disk** implementation (Infrastructure)
  behind the abstraction (ADR-0012).
- All responses still obey the M1 envelope + correlation contract (ADR-0008).

### Definition of Done check
- [x] Platform hardening in place; no business logic / endpoints / token issuance.
- [x] No new third-party deps (built-in rate limiting/CORS; one first-party options helper).
- [x] Tests pass — **68 total** (53 unit, 9 integration, 6 architecture).
- [x] Manual smoke: headers present, no `Server` banner, enveloped 429, Swagger loads.
- [x] Decisions/contracts recorded (ADR-0010/0012; Technical Bible §7.1, §8).

### Status
**AWAITING CO-FOUNDER REVIEW.** On approval, next is **M2 — Authentication** — which is
**gated on an approved Authentication Design Review before any code** (05_NEXT_TASKS.md
T-013): identity model, token lifecycle, refresh rotation, role/permission strategy,
account states, password reset, email/OTP, sessions, audit events, future external IdPs.

---

*When approved, move this to the log and promote the next item.*
