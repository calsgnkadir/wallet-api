# 0004 — Reserve an idempotency key before doing the work

**Status:** Accepted

## Context

A client that loses its connection mid-request cannot tell whether the money moved. It
retries, and a naive server pays twice. The money endpoints need to make a retry safe.

## Decision

The money endpoints accept an optional `Idempotency-Key` header. The key is written to
its own row, unique per `(UserId, Key)`, **before** the action runs. Then the action
executes, and the response is stored back on that row.

The ordering is the whole point: reserve first, execute second.

## Consequences

- Concurrent retries cannot both execute: the unique index lets only one insert win; the
  others hit the constraint and read the stored result or get told it is in progress.
  Ten parallel requests with one key move the money once.
- A completed key replays the original response (same transaction id) and is flagged with
  `Idempotency-Replayed: true`.
- Reusing a key for a different body or endpoint is rejected with `409` rather than
  replaying, since silently replaying would swallow a genuine second request. The body is
  fingerprinted with a hash to detect this.
- A failed request releases its key so it can be retried.
- The conflict is resolved by the database's unique index, not by application locking —
  the same tactic used for email uniqueness and balance concurrency.
