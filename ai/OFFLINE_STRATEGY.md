# OFFLINE & CONNECTIVITY STRATEGY

> Drivers operate **on the road**, across highways, ghats, and rural stretches of
> Tamil Nadu where the network drops. The driver app (`/mobile`, Flutter) must
> stay useful and lose **no critical data** when connectivity fails.
>
> This is a design contract for the mobile client and the sync API. No code yet.

---

## 1. Principle

**Offline is a normal state, not an error.** The app is built network-first but
**degrades gracefully**: the driver keeps seeing their active trip, and every
event they generate is **captured locally and reliably delivered later** — never
lost, never silently dropped.

## 2. What must work offline

| Capability | Offline behaviour |
|------------|-------------------|
| View active booking / trip details | Served from local cache |
| Update trip status (picked up, in-transit, delivered) | Queued locally, synced later |
| Capture proof-of-delivery / documents | Stored locally, uploaded on reconnect |
| Record GPS breadcrumbs | Queued locally (see §4) |
| Browse *new* matches / post a load | **Requires network** — clearly indicated |

The UI must **always show connectivity state** and which actions are queued vs
confirmed. The driver should never wonder whether something was saved.

## 3. Local caching

- Cache the **active trip, booking, and the driver's own profile/verification
  state** in a local store on the device.
- Cache is the **read source of truth while offline**; on reconnect it reconciles
  with the server.
- Sensitive cached data is **encrypted at rest on the device** and cleared on
  logout (see `01_PROJECT_RULES.md` §5).

## 4. GPS queue

- Location breadcrumbs feed the `TrackingEvent` entity (see
  `03_TECHNICAL_BIBLE.md`).
- While offline, GPS points are **appended to a durable local queue** with their
  original device timestamps — we do **not** drop points or backfill fake ones.
- On reconnect the queue flushes in order; server stores events with their real
  captured-at times, so the trip's actual path is preserved.
- The queue is **bounded** (size/age cap); when full it drops **oldest** points
  first and records that trimming happened (no silent unbounded growth).

## 5. Retry mechanism

- Every queued mutation (status update, GPS batch, document upload) retries with
  **exponential backoff + jitter**.
- Mutations carry an **idempotency key** so a retry after a partial success does
  **not** double-apply (aligns with `03_TECHNICAL_BIBLE.md` §6 idempotency).
- Retries are capped; a permanently failing item surfaces to the driver and to
  Support, it is never discarded silently (`01_PROJECT_RULES.md` §1: fail loudly).

## 6. Sync on reconnect

On regaining connectivity, the app:

1. **Flushes the outbound queue** (status updates, GPS, documents) in order, using
   idempotency keys.
2. **Pulls server state** for the active trip/booking.
3. **Reconciles conflicts** with a clear rule: for trip lifecycle, the **server's
   authoritative state wins**; the driver is informed if their queued action was
   superseded (e.g. booking cancelled server-side while they were offline).
4. **Confirms in the UI** which queued items are now committed.

## 7. Data integrity guarantees

- **No lost events:** anything the driver did offline is either delivered or
  explicitly surfaced as failed.
- **No duplicates:** idempotency keys make retries safe.
- **Truthful timestamps:** events keep their real captured-at time, not their
  upload time.
- **No silent success or silent loss:** the driver always knows the state.

## 8. Entity touchpoints

Realised through **TrackingEvent**, **Booking**, **Trip**, **Document**, and
**Notification** (see `03_TECHNICAL_BIBLE.md`). This document is the behavioural
contract those flows must honour on the mobile client.

---

*A driver in a dead zone is still doing their job. The app's duty is to remember
everything they did and deliver it faithfully the moment the signal returns.*
