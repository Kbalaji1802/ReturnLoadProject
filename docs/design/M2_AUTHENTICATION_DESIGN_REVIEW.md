# M2 — Authentication Foundation: Design Review

> **Status:** DRAFT — awaiting co-founder approval. **No code is written until this is approved.**
> **Milestone:** M2 (bounded context: **Identity**). Builds on M1 (API contract, ADR-0008)
> and M1.5 (Security Foundation, ADR-0010). **Region:** India / Tamil Nadu (ADR-0004).
>
> **Purpose:** decide *how* authentication and authorization work before implementing them,
> so the identity layer is right the first time and every later module inherits it.

---

## 0. Scope

**In scope (M2):** identity model, registration, login, JWT issuance, refresh-token
rotation & revocation, the 9-role RBAC + policy layer, account lifecycle states,
password + reset + lockout, session management, audit events. OTP and external identity
are **designed now, implemented later**.

**Out of scope (M2):** document uploads / KYC / RC / DL / insurance verification
(that is **M6 / Trust & Safety**, `08_TRUST_AND_SAFETY.md`), business modules, payments.
See §11 (Identity Verification Roadmap) for why auth and verification are deliberately split.

**Guiding rule:** *authenticated ≠ authorized ≠ verified-to-transact.* These are three
separate gates (`01_PROJECT_RULES.md` §5).

---

## 1. Identity Model

### 1.1 ASP.NET Core Identity — adopt the core, issue our own tokens
**Recommendation:** use **ASP.NET Core Identity** for the *user store and credential
primitives* (password hashing, `UserManager`/`SignInManager`, lockout, security stamps,
token providers for reset/confirm), backed by **EF Core** in the Infrastructure layer —
but **issue our own JWTs** (M1.5 `JwtOptions`) rather than Identity cookies. We do **not**
use Identity UI or cookie auth.

*Why:* Identity gives battle-tested hashing, lockout, and token generation for free
(don't hand-roll crypto — `01_PROJECT_RULES.md`). Our API is token-based for mobile +
admin SPA, so JWT issuance stays ours. This resolves the open item in
`03_TECHNICAL_BIBLE.md` §11 ("auth approach: self-managed vs managed provider") in favour
of **self-managed**, and becomes **ADR-0013** on approval.

*Alternative considered:* fully custom identity (rejected — reinvents hashing/lockout/token
plumbing with more risk); a managed IdP e.g. Auth0/Ent\* (rejected for MVP — external
dependency, cost, data-residency questions for India; revisit if SSO demand appears).

### 1.2 Identity data vs profile data — keep Identity lean
`User` is the **authentication anchor** (`03_TECHNICAL_BIBLE.md` §12). It holds **only**
what authentication needs; domain/profile data lives in separate entities keyed by `UserId`.

| Belongs to **Identity** (`ApplicationUser`) | Belongs to a **profile / domain entity** |
|---|---|
| Id (Guid), Email (+ confirmed), PhoneNumber (+ confirmed) | Full name, display name, locale (Tamil/English) |
| PasswordHash, SecurityStamp, ConcurrencyStamp | Address, business/GST details (Shipper) |
| Lockout fields, AccessFailedCount | Carrier association, dispatcher assignments |
| AccountStatus (§4), roles/claims | KYC / verification status (Trust & Safety, **M6**) |
| Created/updated audit stamps | Ratings, operational history |

### 1.3 How Driver / Shipper / staff relate to User
`User` is the root; role-specific data hangs off it (1:1 profile), matching §12:

```
                         ┌─────────────┐
                         │    User     │  (Identity: credentials, status, roles)
                         └──────┬──────┘
        ┌───────────────┬───────┼────────────────┬─────────────────┐
        ▼               ▼       ▼                 ▼                 ▼
  DriverProfile   ShipperProfile   Staff (Ops/Finance/…)   (future profiles)
        │                                   
        └── Driver ──(Association)── Carrier         (Fleet context, M4/M5)
```

- A **User** may hold one or more **roles** (§13) — e.g. a Carrier Owner who also drives.
- **Driver** and **Shipper** are **domain entities** (Fleet / Identity contexts) that
  reference `UserId`; they are **not** subclasses of the Identity user.
- Creating a `User` does **not** create a Driver/Shipper; onboarding into a role is a
  separate, later step. M2 delivers the `User` + roles only; profile entities arrive with
  their modules (M3 User, M4 Driver, M5 Vehicle).

---

## 2. Authentication Strategy

| Decision | Recommendation | Rationale |
|---|---|---|
| **Access token** | JWT, **15 min** (`Jwt:AccessTokenMinutes`, M1.5) | Short window limits blast radius of a leaked token. |
| **Refresh token** | Opaque random (256-bit), **7 days** (`Jwt:RefreshTokenDays`) | Long-lived session without long-lived access. Opaque (not a JWT) so it is revocable server-side. |
| **Storage** | Refresh tokens stored **hashed** (SHA-256) in a `RefreshToken` table; raw value returned to client once | A DB leak must not yield usable tokens. |
| **Rotation** | **One-time-use rotating** refresh tokens — each refresh issues a new one and invalidates the old | Standard OWASP guidance; enables reuse detection. |
| **Reuse detection** | If a **already-used/revoked** refresh token is presented → revoke the **entire token family** for that session and audit it | Detects token theft; forces re-login. |
| **Revocation** | Refresh: delete/flag the DB record (immediate). Access: rely on short TTL; optional **security-stamp / token-version** claim allows immediate global invalidation | Balances statelessness with the ability to kill sessions. |
| **Multi-device** | Each login creates an independent `RefreshToken` (session) row with device metadata | Logging out one device never logs out the others. |

**Token transport:** access token in the `Authorization: Bearer` header (already wired,
M1.5). Refresh token transport (response body vs `HttpOnly` cookie) — **decision to
confirm** (§12): cookie is safer against XSS for the web admin but complicates the mobile
client; recommendation is **response body + secure client storage** for uniformity across
mobile & SPA, revisited if the admin needs cookie-based sessions.

---

## 3. Authorization

- **Roles = the documented 9** (`03_TECHNICAL_BIBLE.md` §13): Platform Admin, Operations,
  Finance, Support, Carrier Owner, Dispatcher, Driver, Shipper, Association Manager.
- **Model:** RBAC now — roles are carried as claims in the JWT and checked per request
  (never "authenticated == authorized", `01_PROJECT_RULES.md` §5).
- **Policy-based authorization:** define **named policies** (e.g. `CanVerifyDocuments`,
  `CanManageCarrierFleet`, `CanBlacklist`) mapped to roles, rather than sprinkling
  `[Authorize(Roles="…")]`. Controllers/endpoints reference **policies**, so the
  role→capability mapping lives in one place.
- **Future fine-grained permissions:** policies are backed by a **permission catalogue**
  (role → set of permission strings). Today the mapping is static/role-based; later we can
  issue **permission claims** and add per-record checks **without changing call sites** —
  the endpoints already ask "does this principal satisfy policy X?". This is the seam that
  avoids a redesign when granular permissions arrive.
- **Multi-tenant scoping** (Carrier data isolation) is enforced as an authorization
  concern (a Carrier Owner sees only their carrier) — designed here, enforced when the
  Fleet context (M4/M5) lands.

---

## 4. Account Lifecycle

`User.AccountStatus` (an auth concern — distinct from *verification*, §11):

| State | Meaning | Can log in? | Can get tokens? |
|---|---|---|---|
| **PendingVerification** | Registered; email/phone not yet confirmed | Yes (limited) | Yes, but restricted scope until confirmed |
| **Active** | Normal operating account | Yes | Yes |
| **Suspended** | Admin/Ops action (investigation, policy) — reversible | No | No |
| **Locked** | Automatic, temporary — too many failed logins | No (until lock expires) | No |
| **Disabled** | Deactivated (user- or admin-initiated) — terminal-ish | No | No |

**Allowed transitions:**
```
              register
                 │
                 ▼
        PendingVerification ──confirm email/phone──▶ Active
                 │                                     │  ▲
                 │ (admin)                    (admin)  │  │ (unlock / lock expiry)
                 ▼                                     ▼  │
              Disabled ◀──────── Suspended ◀────────▶ Locked
                 ▲                   │  (admin lifts)
                 └───────────────────┘
```
- `Locked` is **automatic and temporary** (failed-login lockout, §5); it returns to its
  prior state on expiry/unlock. `Suspended` and `Disabled` are **deliberate, audited**
  admin actions. All transitions are audited (§8) and, where relevant, notify the user.
- **Important:** `Active` means *can authenticate*. It does **not** mean *can transact* —
  that is the separate verification gate (§11).

---

## 5. Password & Recovery

- **Policy:** enforce `PasswordPolicyOptions` from M1.5 (min 12, upper/lower/digit/symbol,
  max 128) at registration and reset.
- **Hashing:** ASP.NET Core Identity's hasher (**PBKDF2-HMAC-SHA256**, high iteration
  count) by default. **Decision to confirm (§12):** optionally swap to **Argon2id** via a
  custom `IPasswordHasher<T>` — stronger against GPU attacks; adds a dependency. Recommend
  starting with the Identity default and recording an ADR if we adopt Argon2id.
- **Reset flow:** request reset → **always return the same generic response** (never reveal
  whether an email exists) → email a **single-use, time-limited (e.g. 30 min) token** →
  user sets a new password → **all existing sessions/refresh tokens revoked** + audited.
- **Account lockout / failed-login policy:** after **N** consecutive failures
  (recommend **5**) lock for a cooldown (recommend **15 min**, escalating). Login errors
  are **generic** ("invalid credentials") to avoid user enumeration. The `sensitive`
  rate-limit policy (M1.5) is applied to login/reset/OTP endpoints. Failed logins are
  audited (§8).

---

## 6. OTP Strategy (designed now, implemented later)

Phone-first fits India; OTP also underpins KYC pillar 1 (`08_TRUST_AND_SAFETY.md`).

| Aspect | Design |
|---|---|
| **Channels** | **SMS OTP** (primary, India) and **Email OTP** (fallback / email confirmation) |
| **Format / expiry** | 6-digit numeric, **5-minute** expiry, single-use |
| **Retry limits** | Max **5** verification attempts per code, then invalidate & require resend |
| **Resend** | Cooldown (e.g. **60 s**) + daily cap per identity |
| **Rate limiting** | The M1.5 `sensitive` policy + per-phone/email throttling |
| **Storage** | Store only a **hash** of the OTP + expiry + attempt count, never the raw code |
| **Provider** | Deferred to the notifications-provider decision (`§11`); abstracted behind an `IOtpSender` so the provider swaps without touching flows |

OTP is **not** in the M2 build; this section is the contract for when it lands (likely with
M6/Notifications). Email confirmation for `PendingVerification → Active` **is** in M2 and
can use the same token machinery.

---

## 7. Session Management

- A **session = one `RefreshToken` family** (issued at login, rotated on refresh), tagged
  with device/user-agent + IP + issued/last-used timestamps.
- **Multiple devices:** independent sessions; listable so a user can see "where am I
  logged in".
- **Logout (this device):** revoke the current refresh-token family; the access token
  expires within ≤15 min.
- **Logout all devices:** revoke **every** refresh-token family for the user **and** bump
  the security-stamp/token-version so outstanding access tokens are rejected immediately.
- **Password change/reset, suspend, disable:** all trigger *logout-all* semantics.

---

## 8. Audit Events

All written to the append-only `AuditLog` (`03_TECHNICAL_BIBLE.md` §12, Trust & Safety §5;
never editable, `01_PROJECT_RULES.md` §6). Each entry: **actor, subject, action, reason,
before/after, timestamp, correlation id** (M1 `traceId`).

- Login **success** / **failure** (with reason category, not credentials)
- Logout / logout-all
- Token **refresh** + **refresh-reuse detected** (security incident)
- Registration + email/phone **confirmation**
- Password **change** / **reset requested** / **reset completed**
- **Role** granted / revoked
- Account **lock** / **unlock** / **suspend** / **disable** / **reactivate**
- OTP issued / verified / failed (when OTP ships)

Auth failures and lockouts also emit the M1.5 `SecurityEvent` structured log for alerting.

---

## 9. External Identity (Future)

Design so Google / Microsoft / Apple sign-in can be added **without redesigning identity**:

- Keep a **`UserExternalLogin`** relation (provider, provider-key, linked `UserId`) —
  ASP.NET Core Identity already models this (`AspNetUserLogins`). We enable it later; the
  schema seam exists from day one.
- **Account linking by verified email:** an external login links to an existing `User`
  only when the provider asserts a **verified** email matching ours; otherwise a new user
  is created. Never auto-merge on unverified email.
- External logins still receive **our** JWT/refresh tokens — downstream authorization,
  sessions, and audit are provider-agnostic and unchanged.
- No provider is integrated in M2; this section fixes the extension points.

---

## 10. Security Considerations

| Area | Decision |
|---|---|
| **Password hashing** | PBKDF2-HMAC-SHA256 (Identity default); Argon2id optional via ADR (§5). |
| **Secret storage** | Env vars now; managed store deferred to the cloud decision (M1.5 §8, `§11`). Signing key fails fast if absent outside Development (M1.5 `JwtOptionsValidator`). |
| **HTTPS** | Required outside Development (M1.5); tokens only ever travel over TLS. |
| **Token signing keys** | **HS256** (symmetric) via `Jwt:SigningKey` for MVP. **Recommendation:** migrate to **asymmetric RS256/ES256** with a published **JWKS** when a managed key store exists — lets resource servers verify without sharing the secret. Recorded as a follow-up ADR. |
| **Key rotation** | Support **multiple valid keys** (current + previous) with a `kid` header so keys rotate without downtime; rotation cadence defined with the managed store. |
| **Brute-force protection** | Failed-login lockout (§5) + M1.5 `sensitive` rate limit + generic errors (no user enumeration) + audit + alerting. |
| **Token theft** | Refresh rotation + reuse detection (§2) + short access TTL + logout-all via security stamp. |
| **PII minimisation** | Identity stores only auth data; profile/KYC PII lives elsewhere, encrypted at rest where legally required (Trust & Safety §1). |

---

## 11. Identity Verification Roadmap (auth ≠ trust) — *added section*

Because this is a logistics platform moving real cargo in India, **authentication** and
**trust verification** are deliberately **separate concerns**, delivered in different
milestones:

| | **Authentication (M2)** | **Trust Verification (M6 / Trust & Safety)** |
|---|---|---|
| **Question it answers** | "Are you who you say, and may you access your account?" | "Are you allowed to *transact* on the platform?" |
| **Mechanism** | Email/phone + password (+ later OTP/SSO); JWT sessions | KYC (Aadhaar/DigiLocker), RC (VAHAN), DL (SARATHI), insurance, permits |
| **State** | `AccountStatus` (§4) | Per-document verification states (`08_TRUST_AND_SAFETY.md`) |
| **Gate it controls** | Can you log in and call the API | Can you be matched / book (a Driver/Shipper is *transactable* only when required docs are `VERIFIED`) |
| **Milestone** | **M2** | **M6** |

**Consequences of the split:**
- A user can be **`Active` (authenticated)** yet **not transactable** (unverified). The API
  must express this cleanly — e.g. authenticated endpoints work, but matching/booking
  endpoints (later milestones) check a **separate verification policy**.
- **Phone OTP** appears in both worlds: it confirms the phone for *auth* **and** feeds KYC
  pillar 1 for *verification*. Designed once (§6), consumed by both.
- M2 must **not** build verification; it must leave a clean seam (a `verification status`
  the Trust & Safety module owns) so M6 plugs in without reworking identity.

---

## 12. Decisions to confirm before implementation

1. **Adopt ASP.NET Core Identity core + own JWTs** (§1.1) → becomes **ADR-0013**
   (resolves the open "auth approach" in `03_TECHNICAL_BIBLE.md` §11).
2. **Refresh-token transport**: response body (recommended) vs HttpOnly cookie (§2).
3. **Password hasher**: Identity PBKDF2 default (recommended) vs Argon2id (§5).
4. **Signing algorithm now**: HS256 for MVP, with a committed path to RS256/JWKS (§10).
5. Confirm the **lockout thresholds** (5 fails / 15 min) and **token lifetimes**
   (15 min / 7 days) as starting values.

## 13. On approval → work breakdown (still no code until approved)
ADR-0013 recorded; then implement in the **Identity** context: `ApplicationUser` + EF
Identity stores & migration · registration + email confirmation · login → JWT + refresh ·
refresh rotation/reuse-detection/revocation · role seeding + policy catalogue · account
lifecycle · password reset · lockout · session list/logout/logout-all · audit events ·
tests (unit + integration + architecture) — every endpoint on the M1 envelope + M1.5
hardening.

---

*Design first, implementation second. This document is the contract M2 will be built to.*
