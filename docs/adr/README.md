# Architecture Decision Records

These are short records of the decisions that shaped this project — what was chosen,
what the alternatives were, and what each choice costs. They exist so the reasoning is
visible without reading every line of code.

Format is a trimmed [Michael Nygard](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
template: context, decision, consequences.

| #                                             | Decision                                             |
| --------------------------------------------- | ---------------------------------------------------- |
| [0001](0001-use-decimal-for-money.md)         | Store money as `decimal`, never `double`             |
| [0002](0002-guid-identifiers.md)              | Identify entities with GUIDs, not sequential ints    |
| [0003](0003-optimistic-concurrency.md)        | Guard balances with optimistic concurrency           |
| [0004](0004-idempotency-keys.md)              | Reserve an idempotency key before doing the work     |
| [0005](0005-immutable-audit-log.md)           | Make the audit log append-only in two layers         |
| [0006](0006-postgres-with-sqlite-tests.md)    | PostgreSQL in production, in-memory SQLite in tests   |
| [0007](0007-rate-limit-auth-endpoints.md)     | Rate limit the authentication endpoints              |
