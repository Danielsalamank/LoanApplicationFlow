# Fundo — Loan Application Flow

> **Demo video:** https://youtu.be/dSFtyfHvHFs

_Versión en español: [README.es.md](README.es.md)_

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

The API contract is browsable at http://localhost:5137/scalar/v1 (development only),
where `POST /api/applications` can be inspected and executed without Postman.

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
| **Returning customer** | Submit an approved application, then submit again with the **same SSN** and a different amount or company. The result page says the application was updated, and the database still holds one customer and one application. |

Check what the external service received at any time:

```bash
curl http://localhost:4000/customers
```

The mock also logs every call: `[external-service] CREATE 555-12-3456 amount=25000`.

## Decisions and what was left out

- **No authentication**, as stated in the assignment.
- **SQLite** instead of SQL Server/PostgreSQL: real transactions with zero setup for the reviewer.
- The background worker polls the outbox and retries a bounded number of times; no message broker.
- **No Docker, no CI, no seed data.** The reasoning for each one is in
  [ARCHITECTURE.md](ARCHITECTURE.md), together with the rest of the trade-offs.

The UI is in English because the product and its fields (SSN, state) are US-specific.
Code comments are written in Spanish; the documentation is available in both languages.
