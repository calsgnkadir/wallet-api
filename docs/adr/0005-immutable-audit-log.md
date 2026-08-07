# 0005 — Make the audit log append-only in two layers

**Status:** Accepted

## Context

Registrations, login attempts and money movements are written to an audit trail. A trail
that can be quietly edited or deleted is worthless — its value is that it can be trusted
after the fact, including against an insider with database access.

## Decision

Enforce append-only in two independent layers:

1. **Application:** the `DbContext` refuses to save an `AuditEvent` in a modified or
   deleted state.
2. **Database:** a PostgreSQL trigger raises an exception on any `UPDATE` or `DELETE` of
   the `AuditEvents` table.

Money events are written in the same transaction as the movement itself.

## Consequences

- Writing money and its audit record together means a transfer cannot succeed without
  leaving a record — they commit or roll back as one.
- Two layers because they cover different threats. The application guard binds only code
  going through this app; the trigger also binds a DBA at a `psql` prompt, a leaked
  connection string, or another service. This was verified by trying `UPDATE`/`DELETE`
  directly against the database and watching all of them fail.
- This is defence in depth: neither layer is trusted to be the only one.
- Rejected rate-limited requests are logged, not written to this table, so a flood cannot
  be used to amplify writes into it (see [0007](0007-rate-limit-auth-endpoints.md)).
- Reading the trail requires the `Admin` role, since it contains other users' activity
  and IP addresses.
