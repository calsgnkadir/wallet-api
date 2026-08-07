# 0006 — PostgreSQL in production, in-memory SQLite in tests

**Status:** Accepted

## Context

The project started on SQLite for convenience, but SQLite lacks a real decimal type and
cannot `ORDER BY` a `DateTimeOffset` — both of which matter for money and history. At the
same time, the test suite should run anywhere with no database to install and no shared
state between tests.

## Decision

Run on **PostgreSQL** in production (money as `numeric(18,2)`, timestamps as `timestamptz`,
ids as `uuid`). Keep the tests on **in-memory SQLite**, swapping the provider in the test
factory so each test gets its own isolated database.

## Consequences

- Production gets a real decimal type and correct ordering; `SUM(Balance)` is exact.
- `dotnet test` needs nothing installed and is deterministic — the suite is safe to run in
  CI and in parallel.
- The two providers differ, so a small amount of provider-aware code exists: the
  `DateTimeOffset`-to-ticks conversion is applied only under SQLite. This is a real
  trade-off — the tests do not exercise the exact PostgreSQL SQL — so the money paths are
  additionally verified by hand against the running PostgreSQL container.
- The SQLite packages live in the test project only, so the production image does not ship
  a database driver it never uses.
