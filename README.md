# Fundo — Loan Application Flow

> **Demo video:** _TODO: paste your public Loom/Jam link here before sending the repository._

A small loan application flow: a Next.js form, a .NET backend where a rule engine
decides approval, transactional persistence, and a background event that pushes the
result to an external service over HTTP.

```
frontend (Next.js :3000) → API (.NET :5137) → SQLite (loan.db)
                                    │
                                    └── outbox → background worker → mock external service (:4000)
```

## Requirements

- .NET SDK 10
- Node.js 20+

No database server is needed: SQLite file, created automatically on first run.

## Run everything locally

Three terminals, from the repository root.

```bash
# 1. Mock external service — http://localhost:4000
cd mock-service
npm install
npm start

# 2. Backend API — http://localhost:5137
dotnet run --project backend/Loan.Api --launch-profile http

# 3. Frontend — http://localhost:3000
cd frontend
npm install
npm run dev
```

Open http://localhost:3000.

To reset the data, stop the API and delete `backend/Loan.Api/loan.db`.

## Run the tests

```bash
dotnet test
```

13 tests: rule engine, returning-customer path, transaction rollback and the HTTP endpoint.

## Test data

| Scenario | What to type |
| --- | --- |
| **Approved** | Any state other than NY and any SSN not blacklisted. Example: state `TX`, SSN `555-12-3456`. |
| **Denied — state** | State `NY`, any SSN. |
| **Denied — blacklisted SSN** | SSN `111-11-1111`, `222-22-2222` or `333-33-3333` (configured in `backend/Loan.Api/appsettings.json` → `BlacklistedSsns`). |
| **Returning customer** | Submit an approved application, then submit again with the **same SSN** and a different amount/company. The result page says the application was updated, and the database still holds one customer and one application. |

Check what the external service received at any time:

```bash
curl http://localhost:4000/customers
```

The mock also logs every call: `[external-service] CREATE 555-12-3456 amount=25000`.

## Notes / left out on purpose

- **No authentication**, as stated in the assignment.
- **SQLite** instead of SQL Server/PostgreSQL: real transactions with zero setup for the reviewer.
- The outbox worker uses simple polling with a retry cap; no message broker. See
  [ARCHITECTURE.md](ARCHITECTURE.md) for the reasoning and the rest of the trade-offs.
