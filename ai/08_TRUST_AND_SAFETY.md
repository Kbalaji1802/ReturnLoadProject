# 08 — TRUST & SAFETY

> How we keep bad actors, unsafe vehicles, and fraudulent cargo off the platform.
> In a marketplace that moves **real cargo on real roads**, trust is the product.
> Verification is a **pre-trip gate**, not an afterthought — ratings (see
> `02_BUSINESS_BIBLE.md`) are the *post-trip* signal; this document is the
> *pre-trip* one.
>
> **Scope:** India / Tamil Nadu (see `06_DECISION_LOG.md` ADR-0004). All
> verification sources and document types below are the Indian equivalents.

---

## 1. Principles

1. **No unverified party transacts.** A driver cannot be matched, and a shipper
   cannot book, until the verifications required for their role pass.
2. **Verify the document AND the person AND the vehicle.** A valid RC on a truck
   driven by an unverified person is not trust.
3. **Documents expire.** Verification is a *state with an expiry*, never a
   permanent "verified" flag.
4. **Every verification action is audited.** Who checked what, when, and the
   outcome — captured in the `AuditLog` (see `03_TECHNICAL_BIBLE.md`).
5. **Fail closed.** If a verification source is unreachable or ambiguous, the
   party stays `PENDING`, never silently `VERIFIED`.
6. **Minimise stored PII.** Store verification *status* and document references;
   store raw identity documents only where legally required, encrypted at rest.

## 2. Verification pillars (MVP)

Each pillar has: what it proves, the authoritative source, what we store, and its
expiry behaviour.

| # | Pillar | Proves | Source (India) | We store | Expiry |
|---|--------|--------|----------------|----------|--------|
| 1 | **Driver KYC** | The person is real & identifiable | Aadhaar (via authorised KYC / DigiLocker), PAN, phone OTP | Masked ID reference, KYC status, verified name | Re-KYC on policy change |
| 2 | **RC verification** | The vehicle is registered & roadworthy on paper | VAHAN (Parivahan) / DigiLocker | RC number, owner name, vehicle class, status | Follows RC / fitness validity |
| 3 | **Insurance verification** | The vehicle is insured | Insurer policy / DigiLocker | Policy number, insurer, validity dates | **Policy end date** |
| 4 | **Driving licence verification** | The driver is legally licensed for the vehicle class | SARATHI (Parivahan) / DigiLocker | DL number, class(es), validity | **DL expiry date** |
| 5 | **Permit verification** | Goods-carriage & route permits are valid | State/National permit, Fitness Certificate (FC), PUC | Permit type, number, validity, region | **Earliest expiring of the set** |

> **Vehicle-class ↔ licence-class compatibility is mandatory.** The matching
> engine (`MATCHING_ENGINE.md`) must reject a driver whose DL class does not
> cover the vehicle they are operating.

### Verification states (per document / per party)

```
NOT_SUBMITTED → SUBMITTED → UNDER_REVIEW → VERIFIED
                                    │
                                    ├──→ REJECTED (with reason)
                                    └──→ EXPIRED   (auto, on expiry date)
```

A party is **transactable** only when *all* verifications required for its role
are in state `VERIFIED` and none are `EXPIRED`.

## 3. Fraud reporting

- Any user (driver, shipper, ops) can **report** a party, a load, or a booking.
- A report creates a record with: reporter, subject, category (fake docs,
  no-show, cargo theft, payment fraud, harassment, other), evidence, timestamp.
- Reports route to the **Operations** and **Support** roles (see
  `03_TECHNICAL_BIBLE.md` roles) for triage.
- A subject under investigation can be **soft-suspended** (cannot be matched)
  pending review — an explicit, audited action, never silent.

## 4. Blacklist

- A **Blacklist** is a hard block on a person, a vehicle (by RC), or an
  organisation (carrier) from transacting on the platform.
- Blacklisting is a deliberate, **audited, reason-required** action taken by
  Operations/Platform Admin — never automatic.
- Enforcement points: registration, verification, matching, and booking. A
  blacklisted RC cannot be re-registered under a different account.
- Blacklist entries are append-only; lifting a block is a **new** audited action,
  not a deletion.

## 5. Audit trail

Every trust-relevant action writes an immutable `AuditLog` entry:

- Verification submitted / approved / rejected / expired.
- Fraud report filed / triaged / resolved.
- Blacklist added / lifted.
- Manual status overrides by Ops/Admin.

Each entry records **actor, subject, action, reason, before/after state, and
timestamp**. Audit logs are append-only and never editable (see
`01_PROJECT_RULES.md` §6).

## 6. Document expiry reminders

Because DLs, insurance, permits, FC, and PUC all expire, the platform proactively
protects liquidity and safety:

- **Scheduled checks** scan `Document` expiry dates.
- **Reminders** are sent to the owning party (and their Dispatcher/Carrier Owner
  where relevant) at **T-30, T-15, T-7, and T-1 days** before expiry.
- On the expiry date, the document auto-transitions to `EXPIRED`, which flips the
  party to **non-transactable** until re-verified.
- Expiry events are audited and feed the `Notification` module.

## 7. Responsibility mapping (roles)

| Action | Owning role(s) |
|--------|----------------|
| Submit documents | Driver, Carrier Owner, Dispatcher, Shipper |
| Review / approve / reject | Operations |
| Fraud triage | Operations, Support |
| Blacklist / un-blacklist | Platform Admin, Operations |
| Policy definition | Platform Admin |

(See `03_TECHNICAL_BIBLE.md` for the full role list.)

## 8. Data-model touchpoints

This document is realised primarily through these entities (defined in
`03_TECHNICAL_BIBLE.md`): **Document**, **Vehicle**, **Driver**, **Carrier**,
**Association**, **AuditLog**, **Notification**. No new schema is implemented yet —
this is the design contract those entities must satisfy.

---

*A single fraudulent trip destroys more trust than a hundred smooth ones build.
When in doubt, keep the party `PENDING` and escalate to Operations.*
