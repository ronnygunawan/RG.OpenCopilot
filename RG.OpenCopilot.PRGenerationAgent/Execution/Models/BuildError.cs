namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class BuildError {
    public string ErrorCode { get; init; } = "";
    public string Message { get; init; } = "";
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public ErrorSeverity Severity { get; init; }
    public ErrorCategory Category { get; init; }
}
