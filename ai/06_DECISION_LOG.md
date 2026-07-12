# 06 — DECISION LOG

> An append-only journal of **every significant decision** and *why* we made it.
> This is our institutional memory. Future-us (and future agents) will thank us.
>
> Format is a lightweight **ADR (Architecture Decision Record)**. Newest at top.
> Never rewrite history — if a decision changes, add a **new** entry that
> supersedes the old one and mark the old one `Superseded`.

---

## Entry template (copy for each new decision)

```
### ADR-XXXX — <short title>
- **Date:** YYYY-MM-DD
- **Status:** Proposed | Accepted | Superseded by ADR-YYYY | Rejected
- **Context:** What problem/decision are we facing? What constraints apply?
- **Decision:** What did we choose?
- **Alternatives considered:** What else, and why not?
- **Consequences:** Trade-offs, follow-ups, and what this locks in or unlocks.
```

---

## ADR-0007 — Defer AutoMapper from the foundation (security + licensing)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** The bootstrap stack named AutoMapper for object mapping. During the
  T-001 build, the CI dependency audit (`NuGetAudit`, treated as an error per
  `01_PROJECT_RULES.md` §5) flagged the free AutoMapper line with a **high-severity
  DoS advisory (CVE-2026-32933 / GHSA-rvv3-g6hj-g44x)**. The patched releases
  (>= 15.1.1) are under AutoMapper's **new commercial licence**. The foundation has
  **zero mappings**, so nothing depends on it yet.
- **Decision:** Do **not** reference AutoMapper in the foundation. Introduce an
  object-mapping approach when the first real mapping is needed, choosing then
  between a vetted free library (e.g. Mapster), explicit hand-mapping, or a
  licensed AutoMapper if the business approves the cost. The dependency audit gate
  stays **on**.
- **Alternatives considered:** Pin the vulnerable free AutoMapper 13 (rejected:
  ships a known high-severity vuln to `main`); adopt commercial AutoMapper 15+
  now (rejected: unapproved licence cost for a dependency with no current usage);
  suppress the audit warning (rejected: defeats the security gate).
- **Consequences:** `main` stays free of known-vulnerable dependencies. A small
  future decision (which mapper) is deferred to when it is actually needed, with
  no code to migrate because there are no mappings today. FluentValidation (MIT,
  clean) remains wired in the Application layer.

## ADR-0006 — Backend framework: ASP.NET Core on .NET (Clean Architecture)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** `03_TECHNICAL_BIBLE.md` §11 listed "backend language & framework" as
  an **open** decision (ADR-0003 proposed Node/NestJS, Java/Spring, or Go as
  candidates). The bootstrap task (T-001) locked the choice as **.NET / ASP.NET
  Core**. The brief said ".NET 8", but the only SDK available in the build
  environment is **.NET SDK 9**; targeting net9.0 is fully build- and
  run-verifiable there, whereas net8.0 could only be built, not run.
- **Decision:** The backend is **ASP.NET Core Web API** structured as **Clean
  Architecture** — projects `ReturnLoad.Api` (composition root), `.Application`,
  `.Domain`, `.Infrastructure`, `.Shared`, plus `UnitTests` / `IntegrationTests`.
  Dependencies point inward (Api → Application/Infrastructure → Domain; Shared is
  cross-cutting; Domain depends on nothing). Target framework **net9.0**.
  Persistence is **EF Core + Npgsql (PostgreSQL/PostGIS)**; logging **Serilog**;
  validation **FluentValidation**; docs **Swagger/OpenAPI**; **URL-segment API
  versioning** (`/api/v1`); **JWT bearer** wired as framework-only; **SignalR**
  as a realtime foundation; containerised via a multi-stage **Dockerfile**.
- **Alternatives considered:** Node.js/NestJS, Java/Spring, Go (all viable; .NET
  chosen by the co-founder for team fit and a batteries-included, strongly-typed
  platform). **.NET 8 vs .NET 9:** net9.0 chosen because it is the installed,
  verifiable SDK; revisit if a project constraint requires LTS (.NET 8/10).
- **Consequences:** Resolves the largest open item in §11. Establishes the layer
  boundaries and cross-cutting standards every future backend task inherits. If an
  LTS runtime is later mandated, the multi-targeting change is a small, isolated
  edit to `Directory.Build.props`. Business modules, entities, migrations, and
  auth logic remain **out of scope** until their own tasks (T-002+).

## ADR-0005 — MVP settlement is direct (platform does not hold money)
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** Holding customer funds in India (escrow / pooled accounts) triggers
  significant regulatory obligations (payment-aggregator licensing, RBI norms,
  KYC/AML). We do not want early regulatory complexity to block shipping the core
  marketplace value: matching empty return legs to loads.
- **Decision:** For the MVP, **the platform does not hold or move money.** Driver
  and shipper **settle directly** between themselves. The platform only **records
  the commission owed** to it per completed booking. Online/collected payments are
  a **future** capability, not part of the MVP.
- **Alternatives considered:** Integrating a payment aggregator / escrow from day
  one (rejected for now: regulatory + compliance load, licensing lead time, slows
  the core loop); acting as merchant of record (rejected: even heavier).
- **Consequences:** Fast path to launch with minimal regulatory surface. We accept
  that commission collection is initially a **recorded receivable**, not an
  automated capture, and that reconciliation/settlement UX is manual at first.
  When we add collected payments, that becomes a **new ADR** and revisits
  `03_TECHNICAL_BIBLE.md` §11. Business framing lives in `02_BUSINESS_BIBLE.md` §4.

## ADR-0004 — Lock the initial target market: India / Tamil Nadu
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** The knowledge base carried a latent contradiction — US framing
  ("deadhead miles") alongside an India example (Chennai→Bangalore). Region is a
  root decision: it fixes currency, units, language, tax, payment regulation, and
  compliance, and every future module inherits it. It cannot stay ambiguous.
- **Decision:** The initial target market is:
  - **Country:** India
  - **State:** Tamil Nadu
  - **Initial routes:** within Tamil Nadu (intra-state)
  - **Currency:** INR (₹)
  - **Distance unit:** kilometres (km)
  - **Languages:** Tamil & English
  - **Compliance regime:** Indian regulations (see `08_TRUST_AND_SAFETY.md`)
- **Alternatives considered:** A market-agnostic build (rejected: forces us to
  abstract currency/units/compliance prematurely and delays real value); a broader
  pan-India launch on day one (rejected: dilutes liquidity — a marketplace needs
  density in one region first).
- **Consequences:** Removes ambiguity from every module. Units are km, money is
  INR, UI is Tamil + English, and Trust & Safety uses Indian sources (VAHAN,
  SARATHI/Parivahan, Aadhaar, DigiLocker, GST, permits). Design should remain
  *localisable* so expanding to other states/countries is a configuration effort,
  not a rewrite. `02_BUSINESS_BIBLE.md` is updated to km/INR to match.

## ADR-0001 — Adopt an AI-first knowledge base to govern the project
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** This is a production-grade, long-lived platform built with heavy AI
  agent involvement. We need a single, durable source of truth so any contributor —
  human or AI — starts from the same rules, business context, and technical
  direction.
- **Decision:** Create an `/ai` knowledge base (`00`–`06`) that every session reads
  first: master prompt, project rules, business bible, technical bible, current
  task, next tasks, and this decision log.
- **Alternatives considered:** Scattering context across READMEs and wiki pages
  (rejected: drifts out of sync, no clear entry point); relying on chat history
  (rejected: ephemeral, not versioned).
- **Consequences:** Adds a small documentation discipline. In return we get
  consistent, auditable, resumable work and far less repeated context-setting.

## ADR-0002 — Monorepo with dedicated top-level folders per concern
- **Date:** 2026-07-11
- **Status:** Accepted
- **Context:** We have multiple deliverables (backend, mobile, admin, database,
  infra) that must evolve together and share a domain language.
- **Decision:** Use a single monorepo with clear top-level folders:
  `/ai /backend /mobile /admin /database /docs /docker /scripts`.
- **Alternatives considered:** Multiple separate repositories (rejected for now:
  higher coordination cost, harder atomic cross-cutting changes early on).
- **Consequences:** Simpler cross-cutting changes and shared tooling now; we accept
  we may need to enforce module boundaries and, later, split repos if scale demands.

## ADR-0003 — Proposed baseline stack (NOT yet locked)
- **Date:** 2026-07-11
- **Status:** Proposed
- **Context:** We need a starting direction, but several choices deserve deliberate
  evaluation before we commit code to them.
- **Decision (proposed):** Flutter for `/mobile`, Angular for `/admin`,
  PostgreSQL (+PostGIS) for `/database`, Docker for local environments. Backend
  framework, cloud provider, caching/realtime layer, payments, notifications,
  mapping, and auth approach remain **open**.
- **Alternatives considered:** To be documented per-choice when each is resolved
  (see `03_TECHNICAL_BIBLE.md` §11).
- **Consequences:** Gives us a concrete direction to react to without prematurely
  locking hard-to-reverse choices. Each open item becomes its own ADR once decided.

---

*Discipline: no significant decision is "made" until it appears here as
**Accepted**. If it's not written down, it can be relitigated forever.*
