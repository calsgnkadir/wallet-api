# 0002 — Identify entities with GUIDs, not sequential ints

**Status:** Accepted

## Context

Every entity needs a primary key. The obvious default is an auto-incrementing integer.
But account and transaction ids appear in URLs and API responses, and this is a
financial system where one user must never reach another user's records.

## Decision

Use `Guid` (UUID) primary keys, generated in the application.

## Consequences

- Ids are not guessable. With sequential ints, seeing `/api/accounts/5` invites trying
  `6`, `7`, … to enumerate other records (an IDOR attack). A GUID cannot be walked.
- Ids can be generated before the row is inserted, which simplifies writing an entity
  and its related rows in one unit of work.
- GUIDs are wider than ints and random GUIDs can fragment a clustered index. Not a
  concern at this scale; if it were, a sequential-GUID scheme would address it.
- Endpoints still never trust an id from the client for authorization — the account is
  resolved from the caller's token. The GUID is defence in depth, not the only defence.
