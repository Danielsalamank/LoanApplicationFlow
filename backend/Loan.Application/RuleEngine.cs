using Loan.Domain;

namespace Loan.Application;

public record DecisionResult(bool Approved, string? DenialReason);

/// <summary>
/// Evalúa todas las reglas de rechazo contra los datos de la solicitud.
/// El motor no conoce ninguna regla en concreto: las recibe por inyección, así que
/// agregar una regla nueva no obliga a tocar el motor ni las reglas existentes.
/// </summary>
public class RuleEngine
{
    private readonly IEnumerable<IDenyRule> _rules;

    public RuleEngine(IEnumerable<IDenyRule> rules) => _rules = rules;

    public DecisionResult Decide(LoanApplicationData data)
    {
        foreach (var rule in _rules)
        {
            var reason = rule.Evaluate(data);
            if (reason is not null)
                return new DecisionResult(false, reason);
        }
        return new DecisionResult(true, null);
    }
}
