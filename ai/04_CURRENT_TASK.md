# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M2 — Authentication Foundation** — ✅ **CORE COMPLETE, awaiting review.**
(Design approved: `docs/design/M2_AUTHENTICATION_DESIGN_REVIEW.md`; decisions in **ADR-0013**.
Prior: M0 ✅, M1 API Foundation ✅ / ADR-0008, M1.5 Security Foundation ✅ / ADR-0010.)

### Goal
The clean authentication **core** for the Identity context: self-managed identity
(ASP.NET Core Identity) issuing our **own JWTs**, on the M1 envelope + M1.5 hardening.

### In scope
- `ApplicationUser` (lean) + roles (the 9 of §13) + Identity EF stores + migration.
- Registration, login → JWT access + rotating refresh (response body).
- Refresh rotation + reuse detection + revocation; **multi-device** sessions;
  logout / logout-all.
- Policy-based RBAC (+ permission-catalogue seam); account lifecycle states + admin
  transitions; lockout (5/15 min, reset-on-success, audit every attempt).
- Password change; **password-reset behind an abstraction** (no email delivery yet).
- `ITokenSigner` abstraction (HS256 now, RS256/JWKS later); claims incl. `permissionsVersion`.
- Audit events; tests (unit + integration + architecture).

### Explicitly OUT of scope (later milestones — must plug in, not complicate)
- **SMS OTP**, **email verification**, **document / KYC verification** (Notifications / M6).
- Business modules, payments.

### Delivered
- ASP.NET Core Identity (`ApplicationUser`, `ApplicationRole`) + our own JWTs; EF Identity
  stores; **`M2_Identity` migration** (AspNet* tables incl. `AspNetUserLogins` external seam,
  + `RefreshTokens`).
- Endpoints (all enveloped, ADR-0008; `sensitive` rate-limit on the anonymous ones):
  `POST /api/v1/auth/register|login|refresh` and `logout|logout-all` (authenticated).
- Rotating, hashed, single-use refresh tokens with reuse detection + multi-device;
  lockout (5/15 min, reset-on-success); `ITokenSigner` (HS256) abstraction; claims incl.
  `permissionsVersion`; policy-based RBAC catalogue; audit as security-tagged logs.
- Tests: **86 green** (60 unit, 18 integration on EF InMemory, 8 architecture).

### Known follow-ups (not blockers for the core)
- Live PostgreSQL smoke deferred to the local Docker env (T-010); persistent `AuditLog`
  table lands with the domain model (T-002); role seeding at deploy time.
- `permissionsVersion` is issued + bumped; per-request enforcement check is future work
  (access tokens are short-lived, so logout-all relies on refresh revocation + TTL).

### Status
**AWAITING CO-FOUNDER REVIEW.** Excluded (by instruction): SMS OTP, email verification,
document verification — later milestones.

---

*When approved, move this to the log and promote the next item.*
