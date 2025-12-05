namespace RG.OpenCopilot.Agent;

public sealed class AgentPlan {
    public string ProblemSummary { get; init; } = string.Empty;
    public List<string> Constraints { get; init; } = new();
    public List<PlanStep> Steps { get; init; } = new();
    public List<string> Checklist { get; init; } = new();
    public List<string> FileTargets { get; init; } = new();
}

public sealed class PlanStep {
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public bool Done { get; set; }
}

public enum AgentTaskStatus {
    PendingPlanning,
    Planned,
    Executing,
    Completed,
    Blocked,
    Failed
}

public sealed class AgentTask {
    public string Id { get; init; } = string.Empty;
    public long InstallationId { get; init; }
    public string RepositoryOwner { get; init; } = string.Empty;
    public string RepositoryName { get; init; } = string.Empty;
    public int IssueNumber { get; init; }
    public AgentPlan? Plan { get; set; }
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.PendingPlanning;
}

public sealed class AgentTaskContext {
    public string IssueTitle { get; init; } = string.Empty;
    public string IssueBody { get; init; } = string.Empty;
    public string? InstructionsMarkdown { get; init; }
    public string? RepositorySummary { get; init; }
}

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}
