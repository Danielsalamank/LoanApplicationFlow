using Loan.Domain;
using Microsoft.Extensions.Logging;

namespace Loan.Application;

/// <summary>
/// Puerto: guardado transaccional de una solicitud aprobada.
/// </summary>
public interface ILoanStore
{
    Task<Customer?> FindCustomerBySsnAsync(string ssn, CancellationToken ct);

    /// <summary>
    /// Guarda (inserta o actualiza) cliente y solicitud, y encola el evento saliente
    /// como UNA sola unidad de trabajo: si algo falla, no se persiste nada.
    /// </summary>
    Task SaveApprovedAsync(Customer customer, Domain.Application application, LoanEvent loanEvent, CancellationToken ct);
}

/// <summary>
/// Evento que se entrega en segundo plano al servicio externo.
/// </summary>
public record LoanEvent(Customer Customer, Domain.Application Application, bool IsReturningCustomer);

public record SubmitResult(bool Approved, string? DenialReason, bool IsReturningCustomer);

public class SubmitApplication
{
    private readonly RuleEngine _ruleEngine;
    private readonly ILoanStore _store;
    private readonly ILogger<SubmitApplication> _logger;

    public SubmitApplication(RuleEngine ruleEngine, ILoanStore store, ILogger<SubmitApplication> logger)
    {
        _ruleEngine = ruleEngine;
        _store = store;
        _logger = logger;
    }

    public async Task<SubmitResult> ExecuteAsync(LoanApplicationData data, CancellationToken ct)
    {
        var decision = _ruleEngine.Decide(data);
        if (!decision.Approved)
        {
            // Toda decisión de crédito queda registrada: es el rastro que permite
            // explicar después por qué se rechazó una solicitud concreta.
            _logger.LogInformation(
                "Application denied. Ssn={Ssn} State={State} Amount={Amount} Reason={Reason}",
                Mask(data.Ssn), data.State, data.RequestedAmount, decision.DenialReason);

            return new SubmitResult(false, decision.DenialReason, false);
        }

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

        _logger.LogInformation(
            "Application approved. Ssn={Ssn} State={State} Amount={Amount} ApplicationId={ApplicationId} Returning={Returning}",
            Mask(data.Ssn), data.State, data.RequestedAmount, application.Id, isReturning);

        return new SubmitResult(true, null, isReturning);
    }

    /// <summary>
    /// Deja solo los últimos cuatro dígitos: el SSN completo no debe llegar a los registros.
    /// </summary>
    private static string Mask(string ssn) => $"***-**-{ssn[^4..]}";
}
