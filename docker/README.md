# /docker — Containers & Local Environment

Everything needed to run the platform reproducibly — from a developer laptop to
production — using containers.

## Planned contents

- **Dockerfiles** for each service (backend, and clients where relevant).
- **docker-compose.yml** to bring up the full local stack with one command
  (API + PostgreSQL/PostGIS + cache, once chosen).
- **Environment templates** and container configuration.
- Orchestration/deployment config for staging and production (target TBD).

## Goal

> `git clone` → one command → the whole stack runs locally with seed data.

Reproducible environments eliminate "works on my machine" and make onboarding fast.

## Status

🚧 **Empty by design.** No Dockerfiles or compose files yet. This is an early
Phase-1 task (**T-010** in [`../ai/05_NEXT_TASKS.md`](../ai/05_NEXT_TASKS.md)).

## Rules

- Config via environment variables; secrets never baked into images.
- Local uses synthetic data only.

See [`../ai/03_TECHNICAL_BIBLE.md`](../ai/03_TECHNICAL_BIBLE.md) §8.
