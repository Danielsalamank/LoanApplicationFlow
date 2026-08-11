using Loan.Application;
using Loan.Domain;

namespace Loan.Tests;

public class RuleEngineTests
{
    private static LoanApplicationData Data(string state = "TX", string ssn = "555-55-5555") =>
        new("Ana", "Lopez", "1 Main St", state, "Acme LLC", 10_000m, ssn);

    [Fact]
    public void Approves_when_no_rule_matches()
    {
        var engine = new RuleEngine([new NyStateDenyRule(), new BlacklistedSsnDenyRule(["111-11-1111"])]);

        var result = engine.Decide(Data());

        Assert.True(result.Approved);
        Assert.Null(result.DenialReason);
    }

    [Fact]
    public void Denies_applications_from_new_york()
    {
        var engine = new RuleEngine([new NyStateDenyRule()]);

        var result = engine.Decide(Data(state: "NY"));

        Assert.False(result.Approved);
        Assert.Contains("New York", result.DenialReason);
    }

    [Fact]
    public void Denies_blacklisted_ssn()
    {
        var engine = new RuleEngine([new BlacklistedSsnDenyRule(["111-11-1111"])]);

        var result = engine.Decide(Data(ssn: "111-11-1111"));

        Assert.False(result.Approved);
        Assert.Contains("SSN", result.DenialReason);
    }

    [Fact]
    public void Engine_evaluates_rules_it_does_not_know_about()
    {
        var engine = new RuleEngine([new AmountLimitDenyRule(5_000m)]);

        var result = engine.Decide(Data());

        Assert.False(result.Approved);
    }

    private class AmountLimitDenyRule(decimal max) : IDenyRule
    {
        public string? Evaluate(LoanApplicationData data) =>
            data.RequestedAmount > max ? "Amount above limit." : null;
    }
}
