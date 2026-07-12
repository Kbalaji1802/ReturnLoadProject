# 02 — BUSINESS BIBLE

> The **why** behind the product. Business context, market, users, model, and the
> shared domain language every engineer and agent must use.

---

## 0. Market scope (v1)

Locked in `06_DECISION_LOG.md` ADR-0004. Every module inherits these:

- **Country:** India · **State:** Tamil Nadu · **Initial routes:** within Tamil Nadu.
- **Currency:** INR (₹) · **Distance:** kilometres (km).
- **Languages:** Tamil & English · **Compliance:** Indian regulations.

Design to be *localisable* — expanding to other states is configuration, not a rewrite.

## 1. The problem

Every day, trucks deliver cargo to a destination and then drive back **empty**.
This empty return leg is called **deadhead** (or **backhaul**) mileage.

Deadhead is pure waste:
- **For carriers/drivers:** fuel burned and hours spent earning nothing.
- **For shippers:** they overpay because carriers price in the empty return.
- **For the planet:** avoidable fuel consumption and emissions.

Industry-wide, a large share of truck kilometres are driven empty. Even a small
reduction represents enormous savings and a real sustainability win.

## 2. Our solution

**ReturnLoadPlatform** is a two-sided marketplace that matches a truck's **empty
return leg** with a shipper who needs cargo moved along that same route and time
window.

- A **carrier/driver** posts (or we infer) their empty return route.
- A **shipper** posts a load that needs to move.
- Our **matching engine** finds routes and loads that overlap in geography and
  time, ranks them, and proposes a match.
- The parties **book**, the load is **transported**, and payment is settled.

We turn a wasted empty leg into a paid trip.

## 3. Who we serve (users)

| Persona | What they want | Pain we remove |
|--------|----------------|----------------|
| **Owner-operator driver** | Fill the empty ride home, earn more per trip | Empty return kilometres = lost income |
| **Fleet / carrier dispatcher** | Maximize utilization across many trucks | Trucks idling or running empty |
| **Shipper (SMB or enterprise)** | Move cargo affordably and reliably | Expensive one-way capacity |
| **Broker (future)** | Aggregate loads and capacity | Manual matching, phone/email chaos |
| **Platform admin (internal)** | Operate, moderate, and support the marketplace | No tooling to run the business |

## 4. How we make money (business model)

Primary model: **transaction commission** — a percentage fee on each successfully
matched and completed load.

**MVP settlement (ADR-0005):** the platform **does not hold money**. Driver and
shipper **settle directly**; the platform **records the commission owed** to it.
Collected/online payments come later — this keeps us clear of early payment-
regulation complexity while still capturing every rupee of take rate as a record.

Possible future revenue lines:
- **Subscription tiers** for high-volume fleets and shippers.
- **Premium placement / priority matching.**
- **Value-added services:** insurance, factoring, fuel discounts, verified badges.
- **Data & analytics** for enterprise shippers (lane pricing, capacity forecasts).

We only win when both sides win — our fee comes from value created (a filled
return leg), not from rent-seeking.

## 5. Why now

- Freight is still coordinated by **phone calls, spreadsheets, and brokers**.
- **Smartphones + GPS** are ubiquitous among drivers.
- **Fuel costs and sustainability pressure** make deadhead reduction urgent.
- **Real-time matching** is now technically and economically feasible.

## 6. The core loop (marketplace flywheel)

1. More drivers → more available return capacity.
2. More capacity → cheaper, faster matches for shippers.
3. More shippers → more loads → more earnings for drivers.
4. More earnings → more drivers. **Repeat.**

Our job is to make each turn of this loop faster and more trustworthy.

## 7. What "good" looks like (north-star & key metrics)

- **North star:** *deadhead kilometres eliminated* (empty legs converted into paid trips).
- **Liquidity:** match rate (loads matched ÷ loads posted) and time-to-match.
- **Trust:** completion rate, on-time rate, dispute rate.
- **Growth:** active drivers, active shippers, loads/week, GMV.
- **Unit economics:** take rate, CAC, contribution margin per completed load.

## 8. Ubiquitous domain language (glossary)

Use these terms **exactly** in code, UI, docs, and conversation.

| Term | Meaning |
|------|---------|
| **Carrier** | The company or owner-operator that owns/operates the truck. |
| **Driver** | The person operating the truck (may equal the carrier). |
| **Shipper** | The party with cargo that needs to be moved. |
| **Load** | A shipment posted by a shipper (cargo + origin + destination + window + constraints). |
| **Trip** | A truck's planned movement, including the **return leg** we want to fill. |
| **Return Leg / Deadhead** | The empty leg of a trip we aim to convert into a paid load. |
| **Lane** | An origin→destination corridor (e.g. Chennai→Bangalore). |
| **Match** | A proposed pairing of a Load with a truck's Return Leg. |
| **Booking** | A confirmed, agreed Match both sides committed to. |
| **Bid / Offer** | A price proposal on a Load or Match. |
| **Settlement** | The payment flow after a completed booking (payout + fee). |
| **Rating** | Post-trip trust signal each side leaves for the other. |
| **GMV** | Gross Merchandise Value — total value of goods moved through the platform. |
| **Take rate** | Our commission as a % of GMV. |

## 9. Guardrails (what we will NOT do)

- We will not compromise **safety** for growth (driver hours, cargo legality).
- We will not become a **race-to-the-bottom** price war that starves drivers.
- We will not hoard or misuse **location/PII data**.
- We will not ship features that we cannot **operate and support**.

---

*Every technical decision should trace back to something in this file. If it
doesn't help fill an empty return leg safely and profitably, question it.*
