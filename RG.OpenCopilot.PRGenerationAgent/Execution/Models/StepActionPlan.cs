namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

/// <summary>
/// Detailed action plan for executing a plan step
/// </summary>
public sealed class StepActionPlan {
    public List<CodeAction> Actions { get; init; } = [];
    public List<string> Prerequisites { get; init; } = [];
    public bool RequiresTests { get; init; }
    public string? TestFile { get; init; }
    public string? MainFile { get; init; }
}
