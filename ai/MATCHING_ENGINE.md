# MATCHING ENGINE

> How we pair a shipper's **Load** with a truck's **Return Leg**. This is the
> single most valuable — and highest-risk — module on the platform, and per
> `01_PROJECT_RULES.md` §4 it carries the **highest test coverage**.
>
> **MVP is deterministic and rules-based.** AI/ML scoring is explicitly a *future*
> version (see §6). We ship something correct and explainable first.
>
> **Scope:** India / Tamil Nadu, distances in **kilometres**, times in local
> timezone (see `06_DECISION_LOG.md` ADR-0004).

---

## 1. What the engine does

Given a **Load** (cargo + origin + destination + time window + constraints) and a
pool of candidate **Return Legs** (a truck's empty leg + vehicle + driver), the
engine returns a **ranked list of eligible matches**, or none.

The engine has two stages:

1. **Hard filters (eligibility)** — a candidate either qualifies or it doesn't.
   Anything that fails *any* filter is excluded. No exceptions.
2. **Ranking** — the survivors are ordered so the best match surfaces first.

## 2. MVP hard filters (all must pass)

A Return Leg is eligible for a Load **only if every one of these is true**:

| # | Filter | Rule |
|---|--------|------|
| 1 | **Compatible vehicle type** | The vehicle type supports the load's cargo type (e.g. reefer for perishables, open/flatbed for construction material). |
| 2 | **Sufficient payload capacity** | `vehicle.availableCapacity ≥ load.weight` **and** load dimensions fit the vehicle. |
| 3 | **Pickup within X km** | The load's pickup point is within **X km** of the return leg's route/origin corridor. |
| 4 | **Delivery along intended route** | The load's destination lies along the return leg's intended route corridor (not a detour beyond tolerance). |
| 5 | **Pickup time within Y hours** | The load's pickup window overlaps the return leg's availability within **Y hours**. |
| 6 | **Driver status = Available** | The driver is not already booked, off-duty, or in-transit on another load. |
| 7 | **Driver verified** | Driver KYC + DL are `VERIFIED` and not expired (see `08_TRUST_AND_SAFETY.md`). |
| 8 | **Vehicle verified** | RC + insurance + permit are `VERIFIED` and not expired. |

> Filters 7 and 8 make **Trust & Safety a hard dependency of matching** — an
> unverified party or vehicle is never eligible, regardless of route fit.

### Tunable parameters

| Symbol | Meaning | MVP default | Notes |
|--------|---------|-------------|-------|
| **X** | Max pickup detour distance | *to be set in config* | Per-lane tuning later |
| **Y** | Max pickup time slack | *to be set in config* | Per-lane tuning later |
| **Corridor width** | Route-overlap tolerance (§ filter 4) | *to be set in config* | Uses PostGIS geometry |

X, Y, and corridor width are **configuration, not hard-coded constants** (see
`01_PROJECT_RULES.md`, `03_TECHNICAL_BIBLE.md` §8). They must be adjustable
without a code change.

## 3. MVP ranking

Among eligible candidates, order by a simple, explainable priority:

1. **Least added detour** (pickup + delivery deviation from the return leg).
2. **Best time fit** (smallest wait between availability and pickup window).
3. **Highest driver/vehicle rating** (tie-breaker; see Ratings).

Every proposed match must be **explainable** — we can state *why* it was ranked
where it was. No black boxes in the MVP.

## 4. Inputs & data dependencies

- **Load:** cargo type, weight, dimensions, origin, destination, pickup/delivery
  window, constraints.
- **Return Leg / Trip:** route corridor, availability window, origin/destination.
- **Vehicle:** type, capacity, dimensions, verification status.
- **Driver:** availability status, verification status, rating.
- **Geo:** PostgreSQL + **PostGIS** for corridor overlap, radius, and distance
  (see `03_TECHNICAL_BIBLE.md` §5).

## 5. Outputs & states

- Output is a set of **candidate Matches**, each `PROPOSED` with a rank and a
  human-readable reason.
- A `Match` progresses independently of the engine (proposed → accepted →
  booked); the engine only **proposes**. It never books.
- **Zero eligible candidates is a valid, first-class result** — the caller must
  handle "no match yet," not treat it as an error.

## 6. Future versions (NOT in MVP)

Once we have enough completed-trip data, ranking evolves from rules to
**AI/ML scoring**: learned lane pricing, likelihood-to-accept, on-time
probability, and driver preference modelling. The **hard filters in §2 remain**
— safety and verification are never optimised away.

## 7. Testing expectations

Per `01_PROJECT_RULES.md` §4, this module gets the highest coverage:

- Each hard filter has explicit pass **and** fail tests.
- Boundary tests on X, Y, capacity, and corridor width.
- "No eligible candidate" path is tested.
- Ranking order is tested with fixed fixtures (deterministic, refactor-proof).

---

*A wrong match sends a truck to the wrong place or an unverified driver to real
cargo. Correctness beats cleverness — ship the rules engine, prove it, then learn.*
