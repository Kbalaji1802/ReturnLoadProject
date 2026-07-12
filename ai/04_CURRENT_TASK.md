# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M2 — Authentication Foundation** — 🚧 **IN PROGRESS.**
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

### Status
Building. Contract: every endpoint uses the M1 envelope (ADR-0008) and M1.5 hardening.

---

*When approved, move this to the log and promote the next item.*
