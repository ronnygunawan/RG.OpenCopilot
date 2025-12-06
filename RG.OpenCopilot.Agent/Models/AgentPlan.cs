namespace RG.OpenCopilot.Agent.Models;

public sealed class AgentPlan {
    public string ProblemSummary { get; init; } = "";
    public List<string> Constraints { get; init; } = [];
    public List<PlanStep> Steps { get; init; } = [];
    public List<string> Checklist { get; init; } = [];
    public List<string> FileTargets { get; init; } = [];
}
