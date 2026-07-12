# 01 — PROJECT RULES

> Hard engineering rules. These are **enforced**, not suggested. If you want to
> break one, you must justify it in `06_DECISION_LOG.md` and get sign-off.

---

## 1. Golden rules

1. **No secrets in the repo.** Ever. Not in code, not in config, not in commit
   history. Use environment variables and a secrets manager. `.env` files are
   git-ignored; only `.env.example` is committed.
2. **The `main` branch is always deployable.** If it's broken, fixing it is the
   top priority.
3. **Nothing merges without review** (human or AI) and **green CI**.
4. **Typed at every boundary.** API request/response, DB rows, message payloads,
   and function signatures are explicitly typed. No `any` at boundaries.
5. **Fail loudly.** Never swallow an error silently. Log it, handle it, or
   propagate it — but never hide it.

## 2. Code style & quality

- **One language style per stack**, enforced by an automated formatter and linter.
  A human should never argue about formatting.
- **Names say what they mean.** No `data2`, `tmp`, `doStuff()`. Domain language
  from `02_BUSINESS_BIBLE.md` is used consistently in code (Load, Trip, Carrier,
  Shipper, Match, etc.).
- **Functions are small and single-purpose.** If you can't name it cleanly, it's
  doing too much.
- **Comments explain _why_, not _what_.** The code already says what.
- **No dead code.** Delete it; git remembers.

## 3. Git & version control

- **Branch naming:** `type/short-description` — e.g. `feat/load-matching`,
  `fix/auth-token-expiry`, `chore/ci-pipeline`.
- **Conventional Commits:** `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`,
  `test:`, `perf:`, `build:`, `ci:`.
- **Small, atomic commits** that each leave the tree in a working state.
- **Pull Requests** describe *what* and *why*, link the task, and list how it was
  verified.

## 4. Testing

- **No feature is Done without tests.** Unit tests for logic, integration tests for
  boundaries, end-to-end for critical user journeys (auth, posting a load,
  matching, booking).
- **Test behavior, not implementation.** Tests should survive a refactor.
- **The matching engine and money-handling code get the highest coverage.** These
  are where bugs cost the most.

## 5. Security

- **Least privilege everywhere** — DB users, API keys, cloud roles.
- **Validate all input** at the edge. Never trust the client (mobile, admin, API).
- **AuthN and AuthZ are separate concerns.** Being logged in ≠ being allowed.
- **Encrypt in transit (TLS) and at rest** for sensitive data (PII, location,
  documents, payment references).
- **Rate-limit and audit** authentication and money-related endpoints.
- **Dependencies are scanned** for known vulnerabilities in CI.

## 6. Data

- **Migrations are the only way to change the schema.** No manual edits to prod DBs.
- **Every table has an audit trail** for who/when on sensitive records.
- **Personally Identifiable Information (PII) is minimized, classified, and
  protected.** We collect only what we need.
- **No destructive operation runs without a backup and a confirmation gate.**

## 7. Documentation

- **If it isn't written down, it doesn't exist.** Decisions go in
  `06_DECISION_LOG.md`; architecture in `03_TECHNICAL_BIBLE.md`.
- **Every service/app has a README** explaining how to run, test, and deploy it.
- **APIs are documented** (OpenAPI / schema-first) and kept in sync with code.

## 8. Working with AI agents

- Agents **read the `/ai` knowledge base first**, every session.
- Agents **do not invent scope.** They work the current task and log decisions.
- Agents **verify before claiming done** and report failures honestly.

---

*These rules exist to make us fast and safe at the same time. Follow them and we
ship confidently; ignore them and we ship bugs.*
