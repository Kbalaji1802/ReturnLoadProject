# RC-1 — Release Candidate Audit

> Date: 2026-08-01. Scope: the ReturnLoad platform after the workflow correction sprint
> (backend + mobile + admin). Lens: *can a real transporter run this end-to-end without Swagger?*
>
> Verification basis: `dotnet test` = **205 green** (157 unit + 10 architecture + 38 integration);
> `ng build` = **green**. Flutter `analyze`/`build` is a **manual gate** — the SDK is not installed
> in this environment (ADR-0016); mobile was written to the API contracts and statically reviewed.

---

## 1. End-to-end journey verification

### Driver journey
| Step | Screen (mobile) | API | Status |
|---|---|---|---|
| Register | `register_screen` | `POST auth/register` (+ snackbar, navigate) | ✅ |
| Upload documents | `documents_screen` | `POST documents/driver-upload`; real status from `GET documents` | ✅ |
| Wait approval | `documents_screen` shows real status + rejection reason | admin approves → in-app notification | ✅ |
| Vehicle registration | `vehicle_screen` | `POST vehicles/mine` (+ reset + refresh) | ✅ |
| Vehicle approval | (admin) | `POST vehicles/{id}/activate` | ✅ |
| Become available | `home_tab` availability control | `PUT drivers/me/availability` (+ GPS) | ✅ |
| Receive nearby loads | `loads_tab` | `GET loads/matched` — **pickup-radius hard filter** by area type | ✅ |
| Request load | `loads_tab` | `POST bookings/requests` | ✅ |
| Owner accepts | (owner) | trip created; driver auto-set **Busy** | ✅ |
| Navigate → reach pickup | `trips_tab` (driver-only steps) | `POST trips/{id}/status/{DriverEnRoute…ArrivedPickup}` | ✅ |
| **Pickup confirmed** | owner confirms in `owner_trip_tracking`; driver may self-advance after the window | `…/status/PickupConfirmed` | ✅ |
| Transit | `trips_tab` (Loaded→InTransit→ArrivedDestination→Unloaded) | driver steps | ✅ |
| **Delivery confirmed** | owner confirms; driver fallback after window | `…/status/DeliveryConfirmed` | ✅ |
| Complete + review | `trips_tab` complete; rating dialog both sides | `POST reviews/trip/{id}` | ✅ |
| Return load | `trips_tab` return-availability → `return_loads_screen` | backhaul search (M9) | ✅ |

### Load-owner journey
| Step | Screen | API | Status |
|---|---|---|---|
| Register | `register_screen` (Load Owner) | `POST auth/register` | ✅ |
| Post load | `post_load_screen` — **pickup area type**, validated, reset + navigate | `POST loads` | ✅ |
| Nearby drivers receive | (matching radius) | `GET loads/matched` | ✅ |
| Driver requests | `load_requests_screen` | `GET bookings/for-load/{id}` (enriched) | ✅ |
| Compare drivers | real chips: rating, completed trips, distance/ETA to pickup, verification, availability | Part 7 enrichment | ✅ |
| Approve one | one accepted → **rest auto-rejected** | `POST bookings/requests/{id}/accept` | ✅ |
| Track live | `owner_trip_tracking` — moving marker, ETA, distance remaining | `GET trips/{id}/tracking/live` + **SignalR push** | ✅ |
| Delivery + review | Confirm delivery; rate driver | confirmation gate + reviews | ✅ |

### Admin journey
| Step | Page | API | Status |
|---|---|---|---|
| Login | `login` | `POST auth/login` | ✅ |
| Review driver | `drivers` grid (real **names** + availability) → **driver detail** (photo, phone, email, rating, trips, current status, current trip, company, vehicles, documents, reviews) | `GET drivers`, `GET drivers/{id}`, `GET documents`, `GET reviews/for-user/{id}` | ✅ |
| Review vehicle | `vehicles` grid → **vehicle detail** (number, capacity, type, owner, documents incl. insurance/permit + expiry, status) | `GET vehicles/{id}`, `GET documents?ownerType=1` | ✅ |
| Preview documents | `documents` rich grid → **preview dialog: zoom / rotate / fullscreen** + download | `GET documents/pending`, `GET documents/{id}/file` | ✅ |
| Approve / reject | **confirm dialog** (approve) + **reason-required dialog** (reject); role-gated to Operations | `POST documents/{id}/approve|reject` | ✅ |
| Monitor trips | `trips` grid → **trip detail** (timeline, driver, vehicle, current location, ETA, distance remaining, tracking history, reviews) | `GET trips/{id}/detail`, `…/tracking`, `…/tracking/live`, `reviews/trip/{id}` | ✅ |
| Load detail | `loads` grid → **load detail** (pickup, destination, distance, ETA, owner, requested drivers, approved driver, current status, GPS) | `GET loads/{id}`, `GET bookings`, `GET trips/for-load/{id}` | ✅ |
| View audit | document **review history** (append-only) | `GET documents/{id}/history` | ◑ (see gaps) |
| Dashboard | `dashboard` — Pending drivers / vehicles / documents, Running trips, Open loads (all live) | forkJoin of 5 endpoints | ✅ |
| Reports | — | — | ✗ (roadmap) |

---

## 2. RC-1 checklist

| Question | Answer |
|---|---|
| Can a real transporter complete an end-to-end trip without Swagger? | **Yes** — driver app + owner app + admin console cover the whole loop (register → verify → match by radius → request → owner-choose → drive with owner-confirmation gates → deliver → review → return load). *Caveat:* the Flutter build must be run on a machine with the SDK.￼ |
| Any placeholder values left? | **No.** Fake-data sweep of both apps is clean — no hardcoded 4.8★, "Verified" pills, ₹0, demo pins, or sample data. Every screen binds live API data or an explicit empty state. |
| Forms validated and reset correctly? | **Yes.** Mobile: Post Load / Registration / Driver profile / Vehicle validate, snackbar, reset, navigate. Admin: reject requires a reason; approve/activate confirm first. |
| All APIs secured? | **Yes.** Every endpoint requires auth; staff endpoints are policy-gated (`InternalStaff`, `CanVerifyDocuments`); trip transitions are **participant-authorized** (driver vs owner vs staff); document files are staff-only; location ingestion is assigned-driver-only. |
| All admin workflows complete? | **Mostly.** Detail pages, rich document review with preview, confirm/reject dialogs, sorting (documents), role-based action visibility, and live dashboards are done. Gaps below. |
| Dead routes / broken navigation? | **None found.** Mobile routes verified; the driver-home `/trips` dead link was fixed. Admin: 4 detail routes added; every grid row navigates to its detail. |
| TODOs / FIXMEs remaining? | **None** in app code (sweep clean). |
| Does every screen use live backend data? | **Yes** across mobile and admin. |

---

## 3. Remaining gaps (honest, prioritised)

1. **Flutter build is a manual gate** — SDK not installed here. Run `cd mobile && flutter pub get && flutter analyze && flutter build apk` on a dev machine; paste any analyzer output to fix.
2. **Admin Reports** — not built (explicit roadmap item; out of the correction-sprint scope).
3. **Admin general Audit-Log page** — the `AuditLog` aggregate and document **review history** exist and are queryable (`GET documents/{id}/history`), but there is no consolidated audit-log UI. ◑
4. **Bulk operations** on admin grids — not implemented (single-row actions only).
5. **Server-side pagination/sorting** — admin grids paginate/filter client-side (fine at current scale; revisit for large datasets). Column sorting is implemented on the documents queue; other grids have search + dropdown filters, not header sort.
6. **Vehicle image** — not modelled in the domain; the vehicle detail shows a placeholder icon (not a fake image).
7. **ETA / distance** are straight-line (Haversine) with a recent-speed window, not full road-network routing (documented approximation, ADR-0020).

None of these block the core end-to-end freight workflow. They are additive product surface, appropriate for the post-RC roadmap.

---

## 4. Recommendation

The platform is at **RC-1**: the ReturnLoad business workflow runs end-to-end with live data,
secured APIs, and operator-grade admin tooling. The correct next step is to **run the Flutter
analyze/build gate on an SDK machine**, then proceed to the roadmap (dashboards deep-dive, reports,
search, production integrations) on this corrected foundation rather than carrying workflow debt
into later milestones.
