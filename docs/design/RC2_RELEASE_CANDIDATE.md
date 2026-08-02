# RC-2 — Release Candidate 2 status

> Converting the MVP into a deployable, pilot-ready logistics platform.
> Decisions in **ADR-0021** (this sprint) and **ADR-0020** (the correction sprint before it).
>
> **This document is a status ledger, not a plan.** Every row says what is *actually* true today,
> verified how it says. Nothing is marked done on the strength of code existing — only on a gate
> that was run. Where a part is partly delivered it says which half.

Last updated: 2026-08-02.

---

## 1. Per-part status

Legend — ✅ delivered & verified · 🟡 partly delivered · ⬜ not started

| # | Part | Backend | Client | Notes |
|---|------|---------|--------|-------|
| 1 | Admin document review | ✅ | 🟡 | Rich queue DTO, staff file endpoint, append-only `DocumentReview` history all shipped in ADR-0020. Angular preview dialog exists; zoom/rotate/fullscreen/prev-next and the 20-char reject minimum are **not** implemented. |
| 2 | GPS live tracking | ✅ | 🟡 | SignalR `TrackingHub`, recent-speed ETA, tracking history (ADR-0019/0020). Driver app does **not** yet ping every 3s; admin/owner maps still placeholder. |
| 3 | Geo radius matching | ✅ | n/a | Hard filter per area type (ADR-0020) **plus** the time-escalating ladder added here. See §3 for the 20-vs-25km discrepancy. |
| 4 | Dynamic trip workflow | ✅ | 🟡 | Owner-confirmation gates, participant authorisation, illegal transitions rejected (ADR-0020). Client still allows button spam pending the form work in Part 5. |
| 5 | Form experience | n/a | 🟡 | Post-load resets on success. Not audited across every create/edit form. |
| 6 | Driver availability | ✅ | ✅ | Five states, `Busy` system-managed, only `Available` matched (ADR-0020). |
| 7 | Notifications | ✅ | 🟡 | Aggregate, unread count, mark read/all. Badge, deep-link and polling not verified end-to-end. |
| 8 | Dashboards | 🟡 | 🟡 | Driver and owner dashboards read real data. **No charts anywhere**; admin dashboard lacks the counts listed in the brief. |
| 9 | Vehicle management | ⬜ | ⬜ | Vehicle images, per-document expiry alerts and history not implemented. Expiry *notifications* now exist generically (Part 13). |
| 10 | Global search | ⬜ | ⬜ | Not started. |
| 11 | Reports (CSV/Excel/PDF) | ⬜ | ⬜ | Not started. No reporting dependency chosen. |
| 12 | Audit UI | 🟡 | ⬜ | `AuditLog` aggregate exists; no UI surfaces it. |
| 13 | Document expiry | ✅ | n/a | 30/15/7/1/expired, config-driven, idempotent. Delivered here. |
| 14 | Company management | 🟡 | ⬜ | `Carrier` + `Association` exist. No Branch or Dispatcher model. |
| 15 | Production polish | n/a | 🟡 | Skeletons and empty states exist in places; not swept. |
| 16 | Redis | ⬜ | n/a | Not started. Deliberately last — see §4. |
| 17 | Background jobs | 🟡 | n/a | Expiry worker delivered. Cleanup, statistics and reminder jobs not started. |
| 18 | Security review | 🟡 | — | RBAC, policies, rate limits, JWT/refresh, CORS, validation all in place from M1.5/M2. **No dedicated RC-2 audit has been run.** |
| 19 | Release readiness | ✅ | ✅ | All five gates run and green — see §2. Now enforced by CI on every push. |
| 20 | Final workflow audit | 🟡 | 🟡 | Driver→documents→approve→verified and owner→post→book→accept→complete verified against the deployed API. Return-load and review legs not walked. |

---

## 2. Release gates (RC-2 Part 19)

Run locally on 2026-08-02, all green, and now enforced by `.github/workflows/ci.yml` on push:

| Gate | Result |
|------|--------|
| `dotnet build` (Release) | ✅ 0 warnings, 0 errors |
| `dotnet test` | ✅ **231 passed**, 0 failed (169 unit · 52 integration · 10 architecture) |
| `ng build --configuration production` | ✅ bundle within budget |
| `ng test` | ✅ 1 passed |
| `flutter analyze` | ✅ no issues |
| `flutter test` | ✅ 1 passed |
| `flutter build apk --release` | ✅ 53.3 MB |

---

## 3. Known limitations

1. **Highway radius: brief says 20km, ADR-0020 and MATCHING_ENGINE.md say 25km.** The accepted
   25 is kept; the conflict is raised rather than silently resolved. One-line config change either
   way — decide and amend the ADR.
2. **Uploaded documents are ephemeral.** Storage is container-local, so every redeploy wipes them.
   For a platform whose compliance documents are legal records this is the single largest blocker
   to pilot use. Needs object storage or a mounted disk.
3. **No email or phone verification.** Anyone can register under any claimed identity.
4. **Driver verification has no manual path** — only document approval sets a driver Active. No
   admin override for a disputed or mis-scanned document.
5. **Migrations are not exercised by any test.** The integration suite builds its schema with
   `EnsureCreated()`, so migration SQL first executes on deploy. Schema drift would not be caught.
6. **Client test coverage is ~nil** — 1 smoke test in mobile, 1 scaffold spec in admin, against 231
   backend tests. Every client-tier bug found during deployment was in untested code.
7. **The APK is debug-signed** with `applicationId com.example.returnload_mobile`. Play Store will
   reject it, and a debug-signed build cannot be upgraded in place later.
8. **Free-tier hosting.** Render sleeps after ~15 min idle (~50s cold start); no SLA, no backups,
   no error tracking or alerting configured.

---

## 4. Sequencing for the remainder

Ordered by what unblocks pilot use soonest, not by part number:

1. **Persistent document storage** (limitation 2) — blocks everything else being real.
2. **Client tests for admin + mobile** (limitation 6) — the tier where bugs actually occur.
3. **Parts 1/2/5/8/15 client work** — the ADR-0020 remainder; the backend is already there, so
   this is the cheapest visible progress per hour.
4. **Part 9 vehicle documents + Part 12 audit UI** — both mostly surface over existing aggregates.
5. **Parts 10, 11, 14** — new features, each substantial.
6. **Part 16 Redis last.** Caching an app with no measured load is guessing. Introduce it against
   a profile, not a checklist — and never over mutable transactional data.

---

*RC-2 is not complete. This ledger exists so its state is legible without reading the diff.*
