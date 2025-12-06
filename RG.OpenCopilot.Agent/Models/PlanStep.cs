namespace RG.OpenCopilot.Agent.Models;

public sealed class PlanStep {
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Details { get; init; } = "";
    public bool Done { get; set; }
}
