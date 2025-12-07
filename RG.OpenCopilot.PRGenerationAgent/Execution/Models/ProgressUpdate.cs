namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

/// <summary>
/// Represents a progress update for task execution
/// </summary>
public sealed class ProgressUpdate {
    public string Stage { get; init; } = "";
    public string Message { get; init; } = "";
    public ProgressStatus Status { get; init; }
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
}
