# /backend — API Server (.NET 9, Clean Architecture)

The heart of the platform: a versioned ASP.NET Core Web API consumed by the mobile
app, the admin console, and partners. Stack locked in
[`../ai/06_DECISION_LOG.md`](../ai/06_DECISION_LOG.md) **ADR-0006**.

> **Status: foundation only.** No business modules, entities, database tables,
> migrations, or auth logic yet — only the health endpoint is exposed. Business
> modules arrive with tasks T-012+ (see [`../ai/05_NEXT_TASKS.md`](../ai/05_NEXT_TASKS.md)).

## Solution layout (Clean Architecture)

```
ReturnLoad.sln
├─ src/
│  ├─ ReturnLoad.Domain          Enterprise core — depends on nothing. Entity building blocks.
│  ├─ ReturnLoad.Application      Use-case layer — DI wiring, FluentValidation. Depends on Domain, Shared.
│  ├─ ReturnLoad.Infrastructure   EF Core + Npgsql (PostgreSQL/PostGIS), health checks. Depends on Application.
│  ├─ ReturnLoad.Shared           Cross-cutting primitives: Result<T>, ApiResponse, PagedResult.
│  └─ ReturnLoad.Api              Composition root — Serilog, Swagger, versioning, JWT, SignalR, health.
└─ tests/
   ├─ ReturnLoad.UnitTests        xUnit — fast tests for Shared primitives.
   └─ ReturnLoad.IntegrationTests xUnit + WebApplicationFactory — boots the API, hits /health.
```

Dependencies point **inward**: `Api → Application/Infrastructure → Domain`;
`Shared` is referenced by the layers that need it; `Domain` depends on nothing.

## What's wired

- **Configuration & DI** — layered `AddApplication()` / `AddInfrastructure()`; all
  config from env vars / `appsettings*.json` (no secrets committed).
- **Logging** — Serilog structured JSON (`appsettings.json`), request logging.
- **Exception handling** — global `IExceptionHandler` → RFC 7807 ProblemDetails.
- **Swagger / OpenAPI** — served in Development at `/swagger`.
- **API versioning** — URL segment, routes under `/api/v1/...`.
- **Health checks** — `/health/live` (liveness) and `/health/ready` (DB readiness),
  plus a friendly `GET /api/v1/health`.
- **JWT authentication** — bearer scheme wired as **framework only** (no token
  issuance / user store yet — that is task T-013).
- **SignalR** — `NotificationsHub` at `/hubs/notifications` as a realtime foundation.
- **EF Core** — `ApplicationDbContext` with **no DbSets/tables yet** (no migrations).

## Run it

Prerequisites: .NET SDK 9. A PostgreSQL/PostGIS instance is only needed for the
readiness probe and future data work — the easiest is `docker compose` from
[`../docker`](../docker).

```bash
# From backend/
dotnet build ReturnLoad.sln -c Release
dotnet test  ReturnLoad.sln -c Release          # 14 unit + 2 integration tests
dotnet run --project src/ReturnLoad.Api          # then open http://localhost:5271/swagger
```

Health check: `curl http://localhost:5271/health/live` → `Healthy`.

## Quality gates

- `TreatWarningsAsErrors` + NuGet dependency audit as errors (`Directory.Build.props`).
- One style baseline via `.editorconfig`.
- `net9.0`, nullable + implicit usings enabled solution-wide.
