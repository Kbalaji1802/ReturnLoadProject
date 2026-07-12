<div align="center">

# 🚚 ReturnLoadPlatform

**Turning empty return trips into paid loads.**

A production-grade logistics marketplace that connects truck drivers with shippers
to eliminate wasted *deadhead* (empty return) miles.

</div>

---

## What is this?

Every day, trucks deliver cargo and then drive **back empty**. That empty return
leg — *deadhead* — is wasted fuel, wasted driver income, and avoidable emissions.

**ReturnLoadPlatform** matches a truck's empty return leg with a shipper who needs
cargo moved along the same route and time window. We turn a wasted leg into a paid
trip — cheaper capacity for shippers, more income for drivers, less fuel burned for
everyone.

> This is **not a demo**. It is being built as a production-grade platform from day
> one. See [`/ai/00_MASTER_PROMPT.md`](ai/00_MASTER_PROMPT.md) for the operating
> principles.

## Who it's for

- **Drivers & carriers** — fill the empty ride home and earn more per trip.
- **Shippers** — move cargo affordably along existing routes.
- **Platform operators** — run and moderate the marketplace from an admin console.

## Repository layout

This is a **monorepo**. Each top-level folder owns one concern:

| Folder | Purpose |
|--------|---------|
| [`/ai`](ai/) | The project's brain — the AI/engineering knowledge base and matching intelligence. **Start here.** |
| [`/backend`](backend/) | The API server: auth, loads, trips, matching, bookings, settlement. |
| [`/mobile`](mobile/) | The driver (and shipper) mobile app. **Flutter** (scaffolded). |
| [`/admin`](admin/) | Internal operations console. **Angular** (scaffolded). |
| [`/database`](database/) | Schema, migrations, seed data, ERDs. Proposed: PostgreSQL + PostGIS. |
| [`/docs`](docs/) | Human-facing documentation: architecture, API, runbooks. |
| [`/docker`](docker/) | Dockerfiles, compose files, and container/orchestration config. |
| [`/scripts`](scripts/) | Automation for setup, build, deploy, and maintenance. |

## Start here (for humans and AI agents)

Read the knowledge base in [`/ai`](ai/), in order:

1. [`00_MASTER_PROMPT.md`](ai/00_MASTER_PROMPT.md) — who we are, what we build, the rules.
2. [`01_PROJECT_RULES.md`](ai/01_PROJECT_RULES.md) — hard engineering rules.
3. [`02_BUSINESS_BIBLE.md`](ai/02_BUSINESS_BIBLE.md) — market, users, model, domain language.
4. [`03_TECHNICAL_BIBLE.md`](ai/03_TECHNICAL_BIBLE.md) — architecture, stack, standards.
5. [`04_CURRENT_TASK.md`](ai/04_CURRENT_TASK.md) — what we're doing right now.
6. [`05_NEXT_TASKS.md`](ai/05_NEXT_TASKS.md) — the prioritized backlog.
7. [`06_DECISION_LOG.md`](ai/06_DECISION_LOG.md) — every significant decision and why.

## Project status

🟢 **Technical foundation bootstrapped (T-001).** The three stacks are scaffolded on
their locked technologies — **.NET 9 / ASP.NET Core** (Clean Architecture, builds +
16 tests green), **Angular** (standalone + Signals + Material), and **Flutter**
(feature-first). Infrastructure is wired: DI, config, Serilog logging, exception
middleware, Swagger, API versioning, health checks, EF Core/PostgreSQL, and Docker.

**Still no business code** — no Driver/Vehicle/Load/Trip/Matching/Payment modules,
no database tables, no migrations, no auth logic. Those begin with the domain model
(T-002) and following tasks. Backend framework is now locked
([`06_DECISION_LOG.md`](ai/06_DECISION_LOG.md) ADR-0006); cloud, cache,
notifications, maps, and auth provider remain open in
[`03_TECHNICAL_BIBLE.md`](ai/03_TECHNICAL_BIBLE.md) §11.

> Quick start: `cd backend && dotnet test` · `cd admin && npm install && npm start`
> · `cd docker && cp .env.example .env && docker compose up --build`.

## Guiding principles

- Production-grade or nothing — no throwaway shortcuts on `main`.
- Security and data integrity are features, not afterthoughts.
- If it isn't written down, it doesn't exist — decisions live in the Decision Log.
- We only win when both sides of the marketplace win.

---

<div align="center">
<sub>Built with intent. See <a href="ai/">/ai</a> before writing a line of code.</sub>
</div>
