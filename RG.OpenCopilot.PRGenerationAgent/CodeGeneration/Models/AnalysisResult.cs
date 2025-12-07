namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class AnalysisResult {
    public bool Success { get; init; }
    public List<QualityIssue> Issues { get; init; } = [];
    public string Output { get; init; } = "";
}
