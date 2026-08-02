# 04 — CURRENT TASK

> The **one** thing we are working on right now. Keep this file short and focused.
> When it's done, move it to the log and pull the next item from `05_NEXT_TASKS.md`.

---

## Active task

**RC-2 — Release Candidate 2: MVP → pilot-ready.** 🟡 **In progress.**
Decisions in **ADR-0021**; full per-part ledger in
[`docs/design/RC2_RELEASE_CANDIDATE.md`](../docs/design/RC2_RELEASE_CANDIDATE.md).

### Delivered and verified this increment
- **Part 3 — radius escalation.** An unaccepted load widens its pickup radius on a configured
  ladder as it ages (`Matching:RadiusEscalation`). Resolved from load age at query time, so no
  state to mutate and no job. Only ever widens. 12 tests.
- **Parts 13 + 17 — document expiry sweep.** Holders warned at 30/15/7/1 days and once after
  lapse; thresholds and cadence config-driven. Idempotent via an append-only
  `DocumentExpiryReminder` per (document, threshold) with a unique index, so the timer cannot
  re-send. `DocumentExpiryWorker` schedules; the due-date rule stays host-free and testable.
  23 tests.
- **Part 19 — CI (closes T-011).** Three jobs (backend/admin/mobile) on every push; the mobile job
  runs the **release APK compile** that a debug build does not. Every command verified locally
  first.
- **All five release gates green:** 231 backend tests, `ng build --configuration production`,
  `ng test`, `flutter analyze` (clean), `flutter test`, `flutter build apk --release`.

### Already delivered by ADR-0020 (do not rebuild)
Backend for Parts 1, 2, 4, 6, 7 — document review DTO/file endpoint/append-only history, SignalR
tracking + ETA, owner-confirmation gates with participant authorisation, driver availability,
notifications. **The outstanding work on these is client-side.**

### Not started
Parts 9 (vehicle documents), 10 (global search), 11 (reports), 12 (audit UI), 14 (branch/
dispatcher), 16 (Redis). Parts 5, 8, 15 partly done client-side.

### Blocking limitation
**Uploaded documents are ephemeral** — container-local storage is wiped on every redeploy. For a
platform whose compliance documents are legal records this outranks every remaining feature.

### Open decision
The RC-2 brief lists the Highway pickup radius at **20km**; ADR-0020 and `MATCHING_ENGINE.md` §2
specify **25km**. The accepted 25 is in force. One-line config change either way — decide and
amend the ADR.

---

*When approved, move this to the log and promote the next item.*
