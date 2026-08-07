# 0007 — Rate limit the authentication endpoints

**Status:** Accepted

## Context

`login` and `register` accept credentials, which makes them the natural target for
brute-force and credential-stuffing. The audit trail already records failed logins;
recording alone does not slow an attacker down.

## Decision

Apply a per-IP fixed-window rate limiter (default 10 requests per minute, configurable)
to the two credential endpoints, using the framework's built-in rate limiter. The
middleware runs **before** authentication. Over the limit returns `429` with a
`Retry-After` header.

## Consequences

- A rejected request never reaches password hashing, so a flood does not burn CPU on
  PBKDF2.
- Partitioning by IP means one attacker's attempts do not lock out other users.
- Rejections go to structured logs, not the audit table, so an attacker cannot amplify
  writes into the database by flooding.
- Only the credential endpoints are limited; the money endpoints are already behind a
  token and are left alone.
- Per-IP is coarse — clients behind one NAT share a bucket, and an attacker with many IPs
  spreads across buckets. It raises the cost of the cheap attack without pretending to
  stop a distributed one.
- The limits are bound through the options pattern, read lazily, so configuration (and
  test overrides) applied after service registration is still seen — the same lazy-config
  discipline used for the JWT signing key.
