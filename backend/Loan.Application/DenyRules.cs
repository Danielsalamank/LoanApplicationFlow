using Loan.Domain;

namespace Loan.Application;

/// <summary>Reglas de rechazo concretas. Agregar una regla = una clase nueva y una línea de registro.</summary>
public class NyStateDenyRule : IDenyRule
{
    public string? Evaluate(LoanApplicationData data) =>
        string.Equals(data.State, "NY", StringComparison.OrdinalIgnoreCase)
            ? "Applications from the state of New York are not accepted."
            : null;
}

public class BlacklistedSsnDenyRule : IDenyRule
{
    private readonly HashSet<string> _blacklist;

    public BlacklistedSsnDenyRule(IEnumerable<string> blacklistedSsns) =>
        _blacklist = new HashSet<string>(blacklistedSsns, StringComparer.OrdinalIgnoreCase);

    public string? Evaluate(LoanApplicationData data) =>
        _blacklist.Contains(data.Ssn)
            ? "The provided SSN is not eligible."
            : null;
}
