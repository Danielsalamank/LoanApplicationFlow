using System.Text.Json;
using Loan.Application;
using Microsoft.EntityFrameworkCore;

namespace Loan.Infrastructure;

/// <summary>
/// Implementación con EF Core del almacén transaccional. Un único SaveChangesAsync
/// equivale a una transacción de base de datos: cliente, solicitud y mensaje del
/// evento se confirman juntos o no se confirma ninguno.
/// </summary>
public class LoanStore : ILoanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LoanDbContext _db;

    public LoanStore(LoanDbContext db) => _db = db;

    public Task<Domain.Customer?> FindCustomerBySsnAsync(string ssn, CancellationToken ct) =>
        _db.Customers
           .Include(c => c.Applications)
           .SingleOrDefaultAsync(c => c.Ssn == ssn, ct);

    public async Task SaveApprovedAsync(Domain.Customer customer, Domain.Application application, LoanEvent loanEvent, CancellationToken ct)
    {
        if (_db.Entry(customer).State == EntityState.Detached)
            _db.Customers.Add(customer);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Payload = JsonSerializer.Serialize(LoanEventPayload.From(loanEvent), JsonOptions),
        });

        await _db.SaveChangesAsync(ct);
    }
}

public record CustomerDto(string FirstName, string LastName, string Address, string State, string CompanyName, string Ssn);
public record ApplicationDto(Guid Id, decimal RequestedAmount, Guid CustomerId);

public record LoanEventPayload(bool IsReturningCustomer, CustomerDto Customer, ApplicationDto Application)
{
    public static LoanEventPayload From(LoanEvent e) => new(
        e.IsReturningCustomer,
        new CustomerDto(e.Customer.FirstName, e.Customer.LastName, e.Customer.Address, e.Customer.State, e.Customer.CompanyName, e.Customer.Ssn),
        new ApplicationDto(e.Application.Id, e.Application.RequestedAmount, e.Application.CustomerId));
}
