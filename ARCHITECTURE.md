# Architecture

_Versión en español: [ARCHITECTURE.es.md](ARCHITECTURE.es.md)_

## Project structure

```
backend/
  Loan.Domain/          Entities (Customer, Application) and the IDenyRule contract. No dependencies.
  Loan.Application/     Use case (SubmitApplication), RuleEngine, the deny rules and the ILoanStore port.
  Loan.Infrastructure/  EF Core DbContext, LoanStore (ILoanStore), outbox table and the background publisher.
  Loan.Api/             ASP.NET controller, request validation, DI composition root.
  Loan.Tests/           xUnit tests.
frontend/               Next.js App Router: form page, /approved, /denied.
mock-service/           Express app standing in for the external service.
```

Dependencies point inward: `Api → Infrastructure → Application → Domain`. The
application layer only knows the `ILoanStore` port, so EF Core, SQLite and the HTTP
client are replaceable without touching business rules. The controller only maps
HTTP to the use case.

## Rule engine

`RuleEngine` receives `IEnumerable<IDenyRule>` and returns the first denial reason it
finds; if no rule matches, the application is approved. The engine knows nothing about
the individual rules.

Adding a rule (open/closed — existing rules are never modified):

```csharp
public class MinimumAmountDenyRule : IDenyRule
{
    public string? Evaluate(LoanApplicationData data) =>
        data.RequestedAmount < 1_000m ? "Minimum amount is $1,000." : null;
}
```

Then one line in `Program.cs`:

```csharp
builder.Services.AddScoped<IDenyRule, MinimumAmountDenyRule>();
```

The blacklist itself lives in configuration (`appsettings.json`), not in code.

## Transaction

`LoanStore.SaveApprovedAsync` writes the customer, the application **and** the outbox
row within a single `SaveChangesAsync`, which EF Core wraps in one database
transaction. Either all three exist or none does:

- If the customer or application insert/update fails → nothing is committed and **no
  event is published**, because the event is a row in the same transaction.
- If the process dies right after the commit → the outbox row survives and the worker
  delivers it on the next poll.
- If the external service is down → only the delivery fails; the database stays
  consistent and the message is retried.

This is why the event is stored instead of sent inline: an HTTP call cannot be part of
a database transaction, so a naive "save then POST" leaves the two systems out of sync
whenever the POST fails.

## Returning customer

The use case looks the customer up by SSN. If it exists, it updates the customer and
its single application in place (EF change tracking) instead of inserting, and the
event is flagged `isReturningCustomer: true`. A unique index on `Ssn` guarantees the
invariant at the database level.

## Background event and external service

`OutboxPublisher` is a `BackgroundService` that polls unprocessed outbox rows every
two seconds, outside the HTTP request that answers the form, and delivers them:

| Case | Call |
| --- | --- |
| New customer | `POST /customers` |
| Returning customer | `PUT /customers/{ssn}` |

Payload:

```json
{
  "isReturningCustomer": false,
  "customer": { "firstName": "...", "lastName": "...", "address": "...", "state": "TX", "companyName": "...", "ssn": "555-12-3456" },
  "application": { "id": "...", "requestedAmount": 25000, "customerId": "..." }
}
```

Design choices:

- **SSN as the external key.** The external service is keyed by SSN, the same natural
  key the domain uses to identify a returning customer, so the contract is idempotent:
  redelivering a message produces the same final state.
- **At-least-once with a retry cap (5 attempts).** Failures are logged in the outbox
  row (`Attempts`, `LastError`) and retried on the next poll. Because the endpoints are
  idempotent, a duplicate delivery is harmless.

## Trade-offs

- **Outbox by polling instead of a message broker.** RabbitMQ/Kafka would add
  infrastructure the assignment does not need; the outbox already provides the
  atomicity guarantee, which is the actual requirement.
- **SQLite.** Real transactions, no server to install. The provider is a single line in
  `Program.cs`, so switching to SQL Server or PostgreSQL is a configuration change.
- **`EnsureCreated()` instead of migrations.** One schema, no versioning history to
  maintain in a take-home; migrations would be the first thing to add for a real
  deployment.
- **No repository interface per entity, no MediatR, no AutoMapper.** One use case does
  not justify those layers.
- **A customer has exactly one application**, as described in the assignment; the
  returning-customer path updates it. Supporting several applications per customer
  would mean deciding which one to update, and that rule was not specified.
- **Denial reasons are returned to the browser.** Fine for the exercise; a real product
  would keep the underlying reason internal (a blacklist hit should not be discoverable
  by probing the form) and show a generic message.

## Extras deliberately left out

The assignment welcomes seed data, Docker, CI or structured logging "only if they earn
their place". I went through each one and none does at this scope:

- **Authentication:** explicitly ruled out by the assignment, and there is no data that
  needs a session behind it.
- **Docker:** running the project is already three commands and there is nothing to
  install, because the database is a SQLite file. A compose file would add files and
  build time to solve a problem that does not exist here.
- **CI:** a pipeline earns its place by keeping working code from breaking over time,
  and this repository will not receive further changes. `dotnet test` serves the same
  purpose for a reviewer.
- **Seed data:** the SSN blacklist lives in configuration and the README states exactly
  what to type for each scenario. Preloading customers would only muddy the
  returning-customer demonstration.
- **Structured logging:** `ILogger` is used where it actually pays off, and nowhere else.
  Two places earn it. First, every credit decision is logged with the rule that denied
  it, the state and the amount — a lender has to be able to explain, months later, why a
  specific application was rejected. Second, the background delivery of the event
  (delivered, failed, attempt count), because that is where something fails outside the
  process and there would be no way to diagnose it otherwise. The SSN is always masked
  down to its last four digits before it reaches a log. Adding Serilog and sinks on top
  of that would be configuration without a reader.
