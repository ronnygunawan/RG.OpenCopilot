using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App;

public sealed class SimplePlannerService : IPlannerService
{
    private readonly ILogger<SimplePlannerService> _logger;

    public SimplePlannerService(ILogger<SimplePlannerService> logger)
    {
        _logger = logger;
    }

    public Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating plan for issue: {IssueTitle}", context.IssueTitle);

        // For POC, create a simple structured plan based on the issue
        var plan = new AgentPlan
        {
            ProblemSummary = $"Implement solution for: {context.IssueTitle}",
            Constraints =
            {
                "Follow existing code style and conventions",
                "Ensure all tests pass",
                "Update documentation if needed"
            },
            Steps =
            {
                new PlanStep
                {
                    Id = "step-1",
                    Title = "Analyze the issue requirements",
                    Details = "Review the issue description and understand what needs to be implemented",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-2",
                    Title = "Identify files to modify",
                    Details = "Locate the relevant files and code sections that need changes",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-3",
                    Title = "Implement the changes",
                    Details = "Make the necessary code changes to address the issue",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-4",
                    Title = "Add or update tests",
                    Details = "Ensure there are tests covering the new functionality",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-5",
                    Title = "Verify the solution",
                    Details = "Run all tests and verify the changes work as expected",
                    Done = false
                }
            },
            Checklist =
            {
                "All code changes are complete",
                "All tests pass",
                "Documentation is updated",
                "Code follows project conventions"
            },
            FileTargets =
            {
                "TBD - will be determined during implementation"
            }
        };

        _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);
        return Task.FromResult(plan);
    }
}
