# 0003 — Guard balances with optimistic concurrency

**Status:** Accepted

## Context

Two requests can try to spend the same balance at the same time. Read-modify-write
without protection loses updates: both read a balance of 500, both subtract 400, both
write, and the account ends at 100 having paid out 800 — money created from nothing.

The options are pessimistic locking (`SELECT ... FOR UPDATE`, hold a row lock for the
transaction) or optimistic concurrency (detect a conflicting write and reject it).

## Decision

Use optimistic concurrency. Each `Account` carries a `RowVersion` GUID marked as a
concurrency token; it is regenerated on every save. EF Core adds the old value to the
`UPDATE ... WHERE`, so an interleaved change makes the update match zero rows and raises
`DbUpdateConcurrencyException`, which the service turns into `409 Conflict`.

## Consequences

- Lost updates are impossible: a 10-way parallel withdrawal test leaves the balance
  exactly consistent with the withdrawals that succeeded, and never negative.
- No locks are held across the request, so there is no lock contention or deadlock risk
  under normal load — the cost of a conflict is paid only when one actually happens.
- The caller must handle `409` and retry. In this project the client decides; a
  production system would usually retry automatically a few times.
- Optimistic is the right default when real conflicts on a single account are rare,
  which they are for a personal wallet.
