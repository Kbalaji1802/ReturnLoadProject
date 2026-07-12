# /scripts — Automation & Tooling

Repeatable automation so common tasks are one command, not a wiki page of steps.

## Planned scripts (examples)

- **setup** — prepare a fresh dev environment.
- **build** — build one or all services.
- **test / lint** — run quality gates locally the same way CI does.
- **db** — run migrations, seed, reset the local database.
- **deploy** — promote to staging / production (once pipelines exist).
- **maintenance** — periodic/data housekeeping tasks.

## Conventions

- Prefer **cross-platform**, well-commented scripts; each prints what it does.
- **No destructive action without a confirmation gate** and, where relevant, a
  backup — per [`../ai/01_PROJECT_RULES.md`](../ai/01_PROJECT_RULES.md) §6.
- Scripts read configuration from environment variables, never hard-coded secrets.

## Status

🚧 **Empty by design.** Scripts are added alongside the features that need them,
starting with local environment setup (**T-010**).
