using Loan.Domain;

namespace Loan.Application;

/// <summary>Puerto: guardado transaccional de una solicitud aprobada.</summary>
public interface ILoanStore
{
    Task<Customer?> FindCustomerBySsnAsync(string ssn, CancellationToken ct);

    /// <summary>
    /// Guarda (inserta o actualiza) cliente y solicitud, y encola el evento saliente
    /// como UNA sola unidad de trabajo: si algo falla, no se persiste nada.
    /// </summary>
    Task SaveApprovedAsync(Customer customer, Domain.Application application, LoanEvent loanEvent, CancellationToken ct);
}

/// <summary>Evento que se entrega en segundo plano al servicio externo.</summary>
public record LoanEvent(Customer Customer, Domain.Application Application, bool IsReturningCustomer);

public record SubmitResult(bool Approved, string? DenialReason, bool IsReturningCustomer);

public class SubmitApplication
{
    private readonly RuleEngine _ruleEngine;
    private readonly ILoanStore _store;

    public SubmitApplication(RuleEngine ruleEngine, ILoanStore store)
    {
        _ruleEngine = ruleEngine;
        _store = store;
    }

    public async Task<SubmitResult> ExecuteAsync(LoanApplicationData data, CancellationToken ct)
    {
        var decision = _ruleEngine.Decide(data);
        if (!decision.Approved)
            return new SubmitResult(false, decision.DenialReason, false);

        var existing = await _store.FindCustomerBySsnAsync(data.Ssn, ct);
        var isReturning = existing is not null;

        Customer customer;
        Domain.Application application;

        if (isReturning)
        {
            // Cliente recurrente: se actualizan sus registros, nunca se duplican.
            customer = existing!;
            customer.FirstName = data.FirstName;
            customer.LastName = data.LastName;
            customer.Address = data.Address;
            customer.State = data.State;
            customer.CompanyName = data.CompanyName;

            application = customer.Applications.Single();
            application.RequestedAmount = data.RequestedAmount;
        }
        else
        {
            customer = new Customer
            {
                FirstName = data.FirstName,
                LastName = data.LastName,
                Address = data.Address,
                State = data.State,
                CompanyName = data.CompanyName,
                Ssn = data.Ssn,
            };
            application = new Domain.Application
            {
                RequestedAmount = data.RequestedAmount,
                CustomerId = customer.Id,
                Customer = customer,
            };
            customer.Applications.Add(application);
        }

        await _store.SaveApprovedAsync(customer, application, new LoanEvent(customer, application, isReturning), ct);

        return new SubmitResult(true, null, isReturning);
    }
}
