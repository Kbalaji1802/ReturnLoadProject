# ReturnLoad — Business Workflow Audit (Correction Sprint)

> Audited: 2026-08-01. Scope: Backend (.NET Clean Architecture), Admin (Angular),
> Mobile (Flutter), against the ReturnLoad vision (`ai/02_BUSINESS_BIBLE.md`,
> `ai/MATCHING_ENGINE.md`) and the correction-sprint requirements.
>
> Lens: *operating a logistics company with 500 trucks.* Each finding is judged by
> whether an operations manager, dispatcher, driver, or load owner could actually
> run the business on it.

This is an audit-of-record. Fixes are tracked in the sprint todo and land per part
with tests, migrations, and ADR updates.

---

## 0. Executive summary

The platform is **technically well-built** — real Clean Architecture, a genuine
guarded trip state machine, an explainable matching *scorer*, secured GPS
ingestion, append-only tracking, and (mostly) API-driven clients. The deviations
are **business-workflow correctness gaps**, not architecture rot. The five most
damaging, in operator terms:

1. **Every driver sees every load.** Pickup radius is only a *ranking* signal, never
   a hard filter. A Madurai driver sees a Tambaram load, just ranked lower. *(Part 2)*
2. **No operational availability.** "Driver status" means *verification* (Pending/
   Active/Suspended/Blocked), not *availability*. A driver mid-trip still receives
   new loads. There is no Available/Busy/Offline/On-Leave/Vehicle-Service. *(Part 3)*
3. **Admins approve documents blind.** The pending-documents API returns only type,
   status, expiry, and a raw owner GUID — no driver name/photo, company, vehicle,
   vehicle number, document number, or upload date, and **no way to view/download the
   file.** Rejection reasons overwrite each other (no history). *(Parts 1, 8)*
4. **Owners choose drivers blind.** The candidate list shows name, vehicle reg, and
   rating only — no completed trips, distance/ETA to pickup, verification, or
   availability. *(Part 7)*
5. **Mobile shows fabricated trust/stats.** Hardcoded 4.8★ rating, "Verified" pills,
   ₹0 earnings, "Active" status, and a blanket "Verified driver & vehicle" on every
   booking request. *(Parts 4, 10)*

---

## PART 2 — Driver matching (pickup radius)  · Severity: CRITICAL

- **Radius is a score, not a filter.** `MatchingService.FindCompatibleLoadsAsync`
  loads *all* `Posted` loads platform-wide (`MatchingService.cs:112`) and scores each.
  `MaxPickupRadiusKm` (default **300**) only feeds a soft proximity score
  (`MatchingScorer.cs:37`); distant drivers are down-ranked, never excluded. With no
  driver location, proximity scores a neutral 0.5 — so unlocated drivers see
  everything (`MatchingScorer.cs:42`).
- **No area-type radius.** Only one global `MaxPickupRadiusKm`. No Urban 5 / Suburban
  10 / Highway 25 concept anywhere; `Load`/`Location` carry no area classification.
- **Filter order wrong / incomplete.** Current: verified driver → verified vehicle →
  compatible vehicle → score. Required: Verified Driver → Verified Vehicle → **Available
  Driver** → Compatible Vehicle → **Pickup Radius** → Score. Steps 3 (available) and
  5 (radius) don't exist.
- **Already correct (keep):** verified-driver + verified-vehicle gating
  (`MatchingService.cs:84,106`); pure compatible-vehicle rules (`MatchingRules.cs`);
  best-fit vehicle + deterministic ranking; Haversine (`MatchingScorer.cs:79`) and
  OSRM road distance/ETA (`Geo/OsrmRouteService.cs`) primitives exist for reuse.

## PART 3 — Driver availability  · Severity: CRITICAL

- **The concept does not exist.** `DriverStatus` = Pending/Active/Suspended/Blocked
  (verification), `private set`, no availability field or transitions
  (`Domain/Identity/Enums.cs`, `DriverProfile.cs`). No Available/Busy/Offline/OnLeave/
  VehicleService in the backend or mobile.
- **Busy drivers still matched.** Matching filters only on verification
  (`MatchingService.cs:84`). The only "busy" guard is a one-active-trip check when a
  driver *initiates* a request (`BookingService.cs:131`) — not reflected in the feed.
- **No mobile UI** to set status (`home_tab.dart` shows a hardcoded `'Active'`).

## PART 1 & 8 — Document verification  · Severity: CRITICAL

- **Pending DTO is threadbare.** `DocumentView` = Id, OwnerType, OwnerId, Type,
  VerificationStatus, ExpiresOn (`DocumentService.cs:21`). Missing for the admin queue:
  driver name, driver **photo (not modeled at all)**, company/carrier, vehicle, vehicle
  number, document number, uploaded date. `ListPendingAsync` does no joins.
- **No file access.** DTO exposes no file URL/storage key → admin cannot View or
  Download; no preview dialog exists in the admin app (no zoom/rotate/fullscreen).
- **Rejection history overwritten.** `RejectionReason` is one mutable scalar
  (`Document.cs:63`); `Reject` overwrites, `Verify` nulls it. No history table, no
  `DocumentRejected` event, no rejected-by/at.
- **Driver can't see the reason.** No endpoint projects `RejectionReason`.
- **Reject doesn't notify.** `RejectAsync` never calls `_notify` (approve does).
- **Re-upload isn't a replacement.** `DriverUpload` creates a new Active doc with no
  archive of the prior one; `Document.Archive()`/`Archived` exist but are never called.
- **Mobile:** `documents_screen.dart` never loads real status; tracks an in-memory map,
  never shows "Rejected" or a reason, offers only a generic Upload.
- **Already correct (keep):** reject reason mandatory at domain level
  (`Document.cs:114`); approve endpoint + policy; expiry/fail-closed rules; self-scoped
  upload ownership; status enum lifecycle.

## PART 5 — Trip lifecycle  · Severity: HIGH

- **State machine is solid (keep).** `Trip.Advance` enforces one legal step, rejects
  skips/reversals with `trip_illegal_transition`, stamps start/complete
  (`Trip.cs:95–136`). Mobile gates buttons to the single legal next action
  (`trips_tab.dart`, `enums.dart:39`). This requirement is *mostly met*.
- **Missing owner-confirmation gates.** Required flow has Owner Confirmation after
  Arrive Pickup (before Loaded) and after Arrive Destination (before Completed). The
  enum has neither; the driver unilaterally advances `ArrivedPickup → Loaded` and
  `Unloaded → Completed`. (`Unloaded` is an extra state vs the required flow.)
- **No participant authorization.** `TripsController.Advance` has only `[Authorize]`;
  `AdvanceAsync` takes no caller identity — **any** logged-in user can drive or cancel
  **any** trip (`TripsController.cs:81`, `TripService.cs:144`). This is the real
  "status can be spammed" hole.

## PART 6 — GPS tracking  · Severity: MEDIUM

- **Already strong (keep):** all five fields captured (lat/lng/bearing/speed/accuracy
  +battery+captured-at); ingestion secured to the assigned driver and only while active;
  stops on completion; owner/admin live view with distance-remaining + ETA; append-only
  history for polyline; device vs server time preserved.
- **No real-time push.** `NotificationsHub` exposes no methods; owner marker relies on
  polling. For a 2–5s "moving marker," positions should push over SignalR.
- **ETA/distance are straight-line.** Haversine to destination + whole-trip average
  speed (`TrackingService.cs:142`), so ETA is null while idle and under-reports on road.
- **Ingestion starts at DriverAccepted**, before movement (arguably should be
  DriverEnRoute).

## PART 7 — Load owner workflow  · Severity: HIGH

- **Candidate list is thin.** `ListForLoadAsync` enriches only DriverName,
  VehicleRegistration, DriverRating (`BookingService.cs:288`). Missing: completed trips,
  distance from pickup, ETA to pickup, verification status, current availability.
  Distance/ETA aren't computable today — driver live location isn't persisted on
  `DriverProfile`.
- **Already correct (keep):** owner accepts one → creates Trip, books load, auto-rejects
  the rest with notifications (`BookingService.cs:189`).

## PART 1 & 11 — Admin app  · Severity: CRITICAL / HIGH

- **Documents grid:** 4 columns (type, owner-label, status, approve/reject). Missing 8
  required columns; no View/Download/preview; reject uses `window.prompt` (functionally
  mandatory but not the intended dialog); no approve/reject confirmation.
- **Dashboard:** missing Pending Vehicles and Running Trips (trips never fetched); a
  card duplicates the active-drivers number. Values are genuinely API-derived (good).
- **All six grids:** client-side pagination/filter only; **no column sorting anywhere**;
  no bulk ops; no preview/confirm dialogs; **role-based visibility built into
  `AuthService` but never used** — approve/reject render for any authenticated user.
  Bookings grid is read-only (no approve/reject).
- **No fabricated data** in admin (good) — but truncated GUIDs stand in for names
  (same root cause as the thin DTOs).

## PART 4 — Form UX (mobile)  · Severity: HIGH

- **Post Load:** no reset, no snackbar (inline `_msg` only), keeps demo values → **re-tap
  re-posts the same load** (`post_load_screen.dart:73`, prefilled `:21`).
- **Registration:** navigates, no snackbar, no field clear (`register_screen.dart:46`).
- **Driver profile:** inline msg, no reset, prefilled licence (`driver_profile_screen.dart`).
- **Documents:** snackbar but local-only status.
- **Profile edit:** doesn't exist (read-only).
- **Already correct (keep):** Vehicle add — snackbar + reset (disposed sheet) + refresh;
  booking approve/reject — snackbar + reload.

## PART 9 & 10 — Dashboards & mobile placeholders  · Severity: CRITICAL

- **Driver home** (`home_tab.dart`): only `_availableLoads` is real; earnings ₹0,
  today's trips 0, vehicle "Active", "Verified" pill are literals. No current trip /
  distance-remaining / ETA / notifications / availability.
- **Owner home** (`load_owner_home_tab.dart`): just buttons; zero API calls; no running
  trips / pending requests / completed loads.
- **Fabricated trust:** hardcoded 4.8★ (`profile_tab.dart:38`), "Verified" pills, and a
  blanket "Verified driver & vehicle" on every request card
  (`load_requests_screen.dart:109`); demo map pins (`map_screen.dart`).
- **Already API-driven (keep):** loads+match score, load details, trips, live tracking,
  notifications, vehicles, my-loads, return loads, reviews, geocoding.

---

## Root causes (fix once, resolve many)

| Root cause | Symptoms across layers |
|---|---|
| Pickup radius never a hard filter | Part 2 feed pollution; unlocated drivers see all |
| No availability concept | Part 3; busy drivers matched; no mobile control; Part 7 missing field |
| Thin document/owner DTOs (no joins, no photo, no file URL) | Parts 1/7/8/11; admin GUIDs; blind approvals; blind owner choice |
| No decision-history / actor capture | Part 8 rejection history; audit of who approved |
| Client forms don't reset + prefilled demo values | Part 4 duplicate posts; fabricated submitted data |
| Hardcoded UI literals | Parts 4/10 fake rating/stats/trust |
| No participant authz on trip transitions | Part 5 status spamming |

## Toolchain / quality-gate note

`dotnet test` + `ng build` are verifiable here. Per **ADR-0016**, the Flutter SDK was
not installed in the build environment; `flutter analyze`/`build` may be a manual gate.
To be confirmed at the quality step.
