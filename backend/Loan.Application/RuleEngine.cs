using Loan.Domain;

namespace Loan.Application;

public record DecisionResult(bool Approved, string? DenialReason);

/// <summary>
/// Evaluates all deny rules against the application data.
/// The engine itself has no rule knowledge: rules arrive via DI, so a new rule
/// never requires touching the engine or existing rules (open/closed).
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
