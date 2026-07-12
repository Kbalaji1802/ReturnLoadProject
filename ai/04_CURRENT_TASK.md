# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**M3 — Core Domain Model** — ✅ **COMPLETE, awaiting review.**
(Decisions in **ADR-0014**. Prior: M0 ✅, M1 ✅/ADR-0008, M1.5 ✅/ADR-0010, M2 ✅/ADR-0013.)

### Goal
Build the business domain model (§12 / T-002) across all bounded contexts, in the
**Domain layer only** — no APIs, controllers, services, EF configs, migrations, matching,
GPS, payments, or notifications delivery.

### Delivered (`backend/src/ReturnLoad.Domain`)
- **Building blocks:** `AggregateRoot<TId>` (+ domain events), `ValueObject`,
  `DomainException`, `Guard`, `IDomainEvent`.
- **Value objects:** MobileNumber, EmailAddress, Money (INR), Weight, Distance,
  GeoCoordinate, Location, TimeWindow, DrivingLicenceNumber, AadhaarNumber (masked),
  GstNumber, VehicleRegistrationNumber, VehicleCapacity, LoadRequirement, ReturnLeg,
  LocationPoint, Rating.
- **Aggregates by context:** Identity (UserProfile, Carrier, DriverProfile, Dispatcher,
  Association) · Fleet (Vehicle) · Documents (Document) · Loads (Load) · Trips (Trip) ·
  Tracking (TrackingEvent) · Reviews (Review) · Administration (AuditLog, Notification).
- **Enums** for every business state; **domain events** (DriverRegistered/Verified,
  VehicleRegistered, DocumentUploaded/Verified, LoadCreated/Posted, TripCreated/Started/
  Completed, CarrierRegistered).
- **Tests:** 138 green total (**110 unit** incl. new domain invariants/VO/rule tests, 18
  integration, 10 architecture — new: value objects sealed + in Domain; events in Domain).

### Explicitly NOT built (by instruction)
Controllers/APIs/services/UI, EF configurations, migrations, repositories, matching, GPS,
payments, notification delivery.

### Status
**AWAITING CO-FOUNDER REVIEW.** Recommended next: **persist the domain** — EF Core
configurations + migration for these entities (see `05_NEXT_TASKS.md`).

---

*When approved, move this to the log and promote the next item.*
