namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class LintResult {
    public bool Success { get; init; }
    public string Tool { get; init; } = "";
    public List<QualityIssue> Issues { get; init; } = [];
    public string Output { get; init; } = "";
}
