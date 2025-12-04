using RG.OpenCopilot.Agent;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AgentPlanTests
{
    [Fact]
    public void AgentPlan_Defaults_AreEmpty()
    {
        var plan = new AgentPlan();

        plan.ProblemSummary.ShouldBe(string.Empty);
        plan.Constraints.ShouldBeEmpty();
        plan.Steps.ShouldBeEmpty();
        plan.Checklist.ShouldBeEmpty();
        plan.FileTargets.ShouldBeEmpty();
    }
}
