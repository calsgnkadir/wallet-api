# WalletApi

A secure digital wallet REST API built with ASP.NET Core — user accounts, balances,
and money transfers with an emphasis on transactional integrity and auditability.

## Tech Stack

- **C# / .NET 10**
- **ASP.NET Core** — Web API (controller-based)
- **Entity Framework Core** — ORM, with SQLite for local development
- **JWT Bearer authentication** — stateless auth with role-based authorization
- **Swagger / OpenAPI** — interactive API documentation

## Getting Started

The JWT signing key is a secret and is deliberately not stored in the repository.
Provide one before the first run — the app refuses to start without it:

```bash
dotnet user-secrets set "Jwt:Key" "<a-random-string-of-at-least-32-bytes>" --project WalletApi
```

Then run the API:

```bash
dotnet run --project WalletApi
```

Open <http://localhost:5139/swagger> to explore the API. Call `POST /api/auth/login`,
copy the returned token into the **Authorize** dialog, and the protected endpoints
become available.

## API

| Method | Endpoint             | Auth  | Description                       |
| ------ | -------------------- | ----- | --------------------------------- |
| GET    | `/api/health`        | —     | Liveness check                    |
| POST   | `/api/auth/register` | —     | Create an account                 |
| POST   | `/api/auth/login`    | —     | Exchange credentials for a JWT    |
| GET    | `/api/auth/me`       | JWT   | Return the current user's profile |

## Security Notes

- Passwords are stored only as PBKDF2 hashes; plaintext is never persisted.
- Login returns the same response whether the account exists or not, and verifies a
  dummy hash when it does not, so response timing cannot be used to enumerate accounts.
- Email uniqueness is enforced by a database index, not just an application check.
- User ids are GUIDs rather than sequential integers, so records cannot be enumerated.
- Dependencies are checked with `dotnet list package --vulnerable --include-transitive`.

## Current Status

- [x] Solution + Web API project
- [x] Swagger UI
- [x] Health check endpoint (`GET /api/health`)
- [x] EF Core + SQLite, `User` entity and initial migration
- [x] Registration, login, and JWT-protected endpoints

## Roadmap

- [ ] Accounts — wallet balance per user
- [ ] Transactions — deposit, withdraw, transfer
- [ ] Audit log — immutable record of every operation
- [ ] Concurrency control (optimistic locking) for concurrent transfers
- [ ] Unit tests (xUnit)
- [ ] Docker + docker compose
