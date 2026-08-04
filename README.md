# WalletApi

A secure digital wallet REST API built with ASP.NET Core — user accounts, balances,
and money transfers with an emphasis on transactional integrity and auditability.

## Tech Stack

- **C# / .NET 10**
- **ASP.NET Core** — Web API (controller-based)
- **Entity Framework Core** — ORM over **PostgreSQL**
- **JWT Bearer authentication** — stateless auth with role-based authorization
- **Docker Compose** — API and database start together
- **Swagger / OpenAPI** — interactive API documentation

## Getting Started

The JWT signing key is a secret and is deliberately not in the repository. Copy the
example environment file and put your own random key in it:

```bash
cp .env.example .env
```

Then start the API and PostgreSQL together:

```bash
docker compose up --build
```

Open <http://localhost:8085/swagger>. Register through `POST /api/auth/register`, call
`POST /api/auth/login`, paste the returned token into the **Authorize** dialog, and the
protected endpoints become available.

### Running without Docker

You need a PostgreSQL instance reachable at the connection string in
`appsettings.json`, then:

```bash
dotnet user-secrets set "Jwt:Key" "<a-random-string-of-at-least-32-bytes>" --project WalletApi
```

```bash
dotnet ef database update --project WalletApi
```

```bash
dotnet run --project WalletApi
```

The app refuses to start when the signing key is missing or shorter than 32 bytes.

## API

| Method | Endpoint                        | Auth | Description                              |
| ------ | ------------------------------- | ---- | ---------------------------------------- |
| GET    | `/api/health`                   | —    | Liveness check                           |
| POST   | `/api/auth/register`            | —    | Create a user and open their wallet      |
| POST   | `/api/auth/login`               | —    | Exchange credentials for a JWT           |
| GET    | `/api/auth/me`                  | JWT  | Return the current user's profile        |
| GET    | `/api/accounts/me`              | JWT  | Return the current user's balance        |
| POST   | `/api/transactions/deposit`     | JWT  | Add funds — accepts `Idempotency-Key`    |
| POST   | `/api/transactions/withdraw`    | JWT  | Remove funds — accepts `Idempotency-Key` |
| POST   | `/api/transactions/transfer`    | JWT  | Transfer by email — accepts `Idempotency-Key` |
| GET    | `/api/transactions`             | JWT  | List the current user's transactions     |
| GET    | `/api/audit`                    | Admin | Read the audit trail                    |

## Tests

```bash
dotnet test
```

56 tests, no external dependencies — the suite swaps PostgreSQL for an in-memory SQLite
database, so it is deterministic, runs in parallel, and needs no Docker.

- **Service tests** cover the money rules directly: rounding, overdraft rejection,
  transfer atomicity, ledger-versus-balance consistency, and the concurrency guard.
- **Endpoint tests** boot the whole application in memory and drive it over HTTP with
  real JWTs, covering registration, login, authorization and the full transfer flow.

## Audit Trail

Every registration, login attempt and money movement is written to an append-only
`AuditEvents` table with the actor, amount, IP address and user agent — including the
attempts that were rejected, since a run of failed logins is exactly what an
investigation looks for.

- Money events are written in the same transaction as the movement itself, so a
  transfer cannot succeed without leaving a record.
- The log is immutable in two places: the `DbContext` refuses to save a modified or
  deleted audit row, and a PostgreSQL trigger raises an exception on `UPDATE` or
  `DELETE`. The second one holds even against a client connecting directly to the
  database.
- Reading it requires the `Admin` role — the records contain other users' activity.
  Set `ADMIN_EMAIL` and `ADMIN_PASSWORD` in `.env` to have an administrator created
  on first start; leave them empty and none is created.

## Idempotency

A client whose connection drops mid-request cannot tell whether the money moved, so it
retries — and naively that pays twice. The three money endpoints accept an optional
`Idempotency-Key` header:

```bash
curl -X POST http://localhost:8085/api/transactions/transfer \
  -H "Authorization: Bearer <token>" \
  -H "Idempotency-Key: 7f3c1e0a-..." \
  -H "Content-Type: application/json" \
  -d '{"toEmail":"someone@example.com","amount":100}'
```

- Repeating the request with the same key replays the original response — same
  transaction id, no second movement — and marks it with `Idempotency-Replayed: true`.
- The key is reserved through a unique index before the work starts, so concurrent
  retries cannot both execute. Ten parallel requests sharing one key move the money once.
- Reusing a key for a different amount or a different endpoint is rejected with `409`
  rather than silently replaying, since that would swallow a genuine second request.
- A request that fails releases its key, so the client can retry with the same one.
- Keys are scoped per user.

## Money Handling

- Balances and amounts use `decimal`, never `double`, so no cent is lost to binary
  floating-point rounding.
- A transfer moves both balances and writes both ledger entries inside a single
  database transaction — it either happens completely or not at all.
- Accounts carry a concurrency token. If two requests try to spend the same balance
  at once, the second one fails with `409 Conflict` instead of overwriting the first.
  A 10-way parallel withdrawal test leaves the balance exactly consistent with the
  transactions that succeeded, and never negative.
- Every movement is recorded with the resulting balance, so history is auditable
  without recomputing it.

## Security Notes

- Passwords are stored only as PBKDF2 hashes; plaintext is never persisted.
- Endpoints act on the account resolved from the caller's token, so one user can
  never read or move another user's money by passing a different id.
- Login returns the same response whether the account exists or not, and verifies a
  dummy hash when it does not, so response timing cannot be used to enumerate accounts.
- Email uniqueness is enforced by a database index, not just an application check.
- User ids are GUIDs rather than sequential integers, so records cannot be enumerated.
- Dependencies are checked with `dotnet list package --vulnerable --include-transitive`.
- The container runs as a non-root user and ships only the runtime — no SDK, no sources.
- The signing key reaches the container through the environment, never through an image
  layer or a committed file.
- Failed logins are recorded with their IP address, and the audit trail cannot be
  rewritten from the application or from a direct database connection.

## Current Status

- [x] Solution + Web API project
- [x] Swagger UI
- [x] Health check endpoint (`GET /api/health`)
- [x] EF Core data layer with migrations
- [x] Registration, login, and JWT-protected endpoints
- [x] Accounts — one wallet balance per user
- [x] Transactions — deposit, withdraw, transfer, and history
- [x] Concurrency control (optimistic locking) for concurrent withdrawals
- [x] Unit and integration tests (xUnit)
- [x] PostgreSQL and Docker Compose
- [x] Idempotency keys so a retried request cannot pay twice
- [x] Append-only audit trail with admin-only access

