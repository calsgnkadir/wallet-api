# WalletApi

A secure digital wallet REST API built with ASP.NET Core — user accounts, balances,
and money transfers with an emphasis on transactional integrity and auditability.

## Tech Stack

- **C# / .NET 10**
- **ASP.NET Core** — Web API (controller-based)
- **Swagger / OpenAPI** — interactive API documentation

## Getting Started

```bash
dotnet run --project WalletApi
```

Then open <http://localhost:5139/swagger> to explore the API.

## Current Status

Project skeleton is up and running:

- [x] Solution + Web API project
- [x] Swagger UI
- [x] Health check endpoint (`GET /api/health`)

## Roadmap

- [ ] Users + JWT authentication (register / login, User & Admin roles)
- [ ] Accounts — wallet balance per user
- [ ] Transactions — deposit, withdraw, transfer
- [ ] Audit log — immutable record of every operation
- [ ] Concurrency control (optimistic locking) for concurrent transfers
- [ ] Unit tests (xUnit)
- [ ] Docker + docker compose
