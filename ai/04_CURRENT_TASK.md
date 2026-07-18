# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M4 — MVP Sprint (application layer + clients)** — 🟢 **Backend + Admin done & verified;
Mobile written (unverified); live click-through pending.** (Decisions in **ADR-0016**.)

### Verified this sprint
- **Backend APIs** (Application services + REST): carriers, drivers (register + list),
  vehicles (register/activate), documents (upload + pending + approve→driver verified +
  reject), loads (post/browse/get/accept), trips (create/get/advance/tracking). Envelope,
  M1.5 hardening, M2 policy authz, `DomainException`→400. **147 tests green** incl. a full
  onboarding→verify→load→trip service+SQLite flow test.
- **Angular admin**: login + guard + interceptor + shell + dashboard/drivers/documents/
  loads/trips/settings. **`ng build` succeeds.**
- **Demo seeding** (Development): admin/carrier/driver/shipper users + roles + demo carrier
  + shipper profile.

### Written but NOT build-verified
- **Flutter mobile** (login → dashboard → loads(accept) → tracking): the **Flutter SDK is
  not installed here**, so it compiles-in-principle but is unverified. Same API contracts.

### Placeholders (external services not configured)
- **Maps/GPS:** tracking screen shows real recorded points + "Maps integration pending API
  key" banner (no fake live map).
- **Payments:** `IPaymentService` interfaces only (ADR-0005). **SMS/OTP / email:** not wired.

### Not yet done
- Live PostgreSQL run + browser/device click-through (Docker is available; see README/report).
- Matching engine, real GPS ingestion, notifications delivery, shipper mobile flows.

### Status
Sprint increment committed. Next: stand up Postgres (docker compose) for a live run, then
the matching engine (M9) or hardening of the onboarding APIs.

---

*When approved, move this to the log and promote the next item.*
