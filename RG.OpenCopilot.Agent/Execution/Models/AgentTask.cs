namespace RG.OpenCopilot.Agent.Execution.Models;

public sealed class AgentTask {
    public string Id { get; init; } = "";
    public long InstallationId { get; init; }
    public string RepositoryOwner { get; init; } = "";
    public string RepositoryName { get; init; } = "";
    public int IssueNumber { get; init; }
    public AgentPlan? Plan { get; set; }
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.PendingPlanning;
}
