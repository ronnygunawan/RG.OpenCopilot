namespace RG.OpenCopilot.Agent;

public sealed class AgentPlan {
    public string ProblemSummary { get; init; } = "";
    public List<string> Constraints { get; init; } = [];
    public List<PlanStep> Steps { get; init; } = [];
    public List<string> Checklist { get; init; } = [];
    public List<string> FileTargets { get; init; } = [];
}

public sealed class PlanStep {
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Details { get; init; } = "";
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
    public string Id { get; init; } = "";
    public long InstallationId { get; init; }
    public string RepositoryOwner { get; init; } = "";
    public string RepositoryName { get; init; } = "";
    public int IssueNumber { get; init; }
    public AgentPlan? Plan { get; set; }
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.PendingPlanning;
}

public sealed class AgentTaskContext {
    public string IssueTitle { get; init; } = "";
    public string IssueBody { get; init; } = "";
    public string? InstructionsMarkdown { get; init; }
    public string? RepositorySummary { get; init; }
}

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}
