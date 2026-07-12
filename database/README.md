# /database — Schema, Migrations & Seed Data

The system of record. This folder owns the data model, versioned migrations, seed
data, and entity-relationship diagrams.

## Planned contents

- **Migrations** — the *only* way the schema changes; versioned and reversible.
- **Seed data** — synthetic data to run the platform locally.
- **ERDs** — entity-relationship diagrams for the core domain.
- **Data dictionary** — table/column meanings tied to the domain glossary.

## Core entities (v1 target)

Carrier · Driver · Shipper · Load · Trip · Return Leg · Match · Booking · Bid ·
Settlement · Rating — as defined in
[`../ai/02_BUSINESS_BIBLE.md`](../ai/02_BUSINESS_BIBLE.md) §8.

## Status

🚧 **Empty by design.** No schema yet. Proposed engine: **PostgreSQL + PostGIS**
(for lanes, routes, and radius matching), pending confirmation in the Decision Log.

## Rules

- Migrations only — no manual edits to any shared/production database.
- Sensitive tables carry an audit trail (who/when).
- PII is minimized, classified, and protected.

See [`../ai/03_TECHNICAL_BIBLE.md`](../ai/03_TECHNICAL_BIBLE.md).
