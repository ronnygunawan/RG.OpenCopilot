namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class BuildResult {
    public bool Success { get; init; }
    public int Attempts { get; init; }
    public string Output { get; init; } = "";
    public List<BuildError> Errors { get; init; } = [];
    public List<CodeFix> FixesApplied { get; init; } = [];
    public TimeSpan Duration { get; init; }
}
