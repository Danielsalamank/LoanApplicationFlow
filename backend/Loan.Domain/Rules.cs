namespace Loan.Domain;

/// <summary>
/// A deny rule. If a rule returns a non-null reason, the application is denied.
/// To add a new rule: create a class implementing this interface and register it in DI.
/// </summary>
public interface IDenyRule
{
    string? Evaluate(LoanApplicationData data);
}

/// <summary>Immutable snapshot of the form data the rules evaluate.</summary>
public record LoanApplicationData(
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn);
