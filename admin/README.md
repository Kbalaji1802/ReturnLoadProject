# /admin — Operations Console (Angular)

Internal web console for platform operators (Ops, Support, Finance, Admin). Built
with Angular standalone components, Angular Material, and Signals.

> **Status: foundation only.** A layout shell + a Signals-driven dashboard that can
> check API connectivity. Business views (verification queue, disputes, users)
> arrive with task **T-031**.

## Structure

```
src/
├─ main.ts                       Standalone bootstrap.
├─ styles.scss                   Global styles + Angular Material (M3) theme.
├─ environments/                 environment.ts (prod) + environment.development.ts.
└─ app/
   ├─ app.ts / app.config.ts / app.routes.ts   Root shell + providers + routes.
   ├─ core/api/                  ApiService (typed HttpClient gateway) + models.
   ├─ shared/                    Cross-feature building blocks.
   ├─ layouts/main-layout/       Toolbar + sidenav frame around a router outlet.
   └─ features/dashboard/        Signals-based dashboard (KPIs + API health check).
```

## Highlights

- **Standalone components** throughout — no NgModules.
- **Signals** for reactive state (`signal`, `computed`) in the dashboard.
- **Zoneless** change detection (`provideZonelessChangeDetection`).
- **Angular Material** M3 theme configured in `styles.scss`.
- **Typed API access** via `core/api/ApiService`, base URL from `environments/`.
- **Lazy-loaded** feature routes nested inside the layout.

## Run it

Requires Node 20+ and npm.

```bash
# From admin/
npm install
npm start                 # ng serve → http://localhost:4200 (uses dev environment)
npm run build             # production build into dist/
```

The dev environment points at `http://localhost:8080/api/v1` (the API from
[`../docker`](../docker)). Use the dashboard's **Check API health** button to verify
connectivity once the backend is running.
