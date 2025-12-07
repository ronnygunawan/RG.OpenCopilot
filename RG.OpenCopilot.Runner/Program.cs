using RG.OpenCopilot.PRGenerationAgent;

if (args.Length == 0) {
    Console.WriteLine("RG.OpenCopilot.Runner - local agent runner stub");
    Console.WriteLine("Usage: dotnet run --project RG.OpenCopilot.Runner -- <issue-title> <issue-body>");
    return;
}

var issueTitle = args.ElementAtOrDefault(0) ?? string.Empty;
var issueBody = args.ElementAtOrDefault(1) ?? string.Empty;

var context = new AgentTaskContext {
    IssueTitle = issueTitle,
    IssueBody = issueBody
};

IPlannerService planner = new ConsoleStubPlannerService();
var plan = await planner.CreatePlanAsync(context);

Console.WriteLine("Generated plan:");
Console.WriteLine($"Summary: {plan.ProblemSummary}");
foreach (var step in plan.Steps) {
    Console.WriteLine($"- [{(step.Done ? 'x' : ' ')}] {step.Title}: {step.Details}");
}

file sealed class ConsoleStubPlannerService : IPlannerService {
    public Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default) {
        var plan = new AgentPlan {
            ProblemSummary = $"Stub plan for: {context.IssueTitle}",
            Steps =
            {
                new PlanStep
                {
                    Id = "step-1",
                    Title = "Review repo locally",
                    Details = "In a real implementation, the planner would analyze the repository and instructions.",
                    Done = false
                }
            }
        };

        return Task.FromResult(plan);
    }
}

