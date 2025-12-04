using RG.OpenCopilot.Agent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPlannerService, StubPlannerService>();
builder.Services.AddSingleton<IExecutorService, StubExecutorService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

// Placeholder GitHub webhook endpoint; will be wired to real logic later.
app.MapPost("/github/webhook", () => Results.Ok());

app.Run();

// Temporary stub implementations so the app builds and runs.
file sealed class StubPlannerService : IPlannerService
{
    public Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default)
    {
        var plan = new AgentPlan
        {
            ProblemSummary = "Stub plan for testing RG.OpenCopilot.App",
            Steps =
            {
                new PlanStep
                {
                    Id = "step-1",
                    Title = "Do nothing",
                    Details = "This is a stub planner; replace with real implementation.",
                    Done = false
                }
            }
        };

        return Task.FromResult(plan);
    }
}

file sealed class StubExecutorService : IExecutorService
{
    public Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        task.Status = AgentTaskStatus.Completed;
        return Task.CompletedTask;
    }
}

