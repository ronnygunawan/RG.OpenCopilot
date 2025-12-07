namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class QualityResult {
    public bool Success { get; init; }
    public List<QualityIssue> Issues { get; init; } = [];
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int FixedCount { get; init; }
    public List<string> ToolsRun { get; init; } = [];
    public TimeSpan Duration { get; init; }
}
