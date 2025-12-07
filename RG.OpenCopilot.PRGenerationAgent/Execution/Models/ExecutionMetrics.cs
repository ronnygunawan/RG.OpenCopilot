namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

/// <summary>
/// Metrics collected during step execution
/// </summary>
public sealed class ExecutionMetrics {
    public int FilesCreated { get; set; }
    public int FilesModified { get; set; }
    public int FilesDeleted { get; set; }
    public int BuildAttempts { get; set; }
    public int TestAttempts { get; set; }
    public int LLMCalls { get; set; }
    public TimeSpan AnalysisDuration { get; set; }
    public TimeSpan CodeGenerationDuration { get; set; }
    public TimeSpan BuildDuration { get; set; }
    public TimeSpan TestDuration { get; set; }
}
