namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

/// <summary>
/// Result of executing a complete plan step
/// </summary>
public sealed class StepExecutionResult {
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<FileChange> Changes { get; init; } = [];
    public BuildResult? BuildResult { get; init; }
    public TestValidationResult? TestResult { get; init; }
    public StepActionPlan? ActionPlan { get; init; }
    public TimeSpan Duration { get; init; }
    public ExecutionMetrics Metrics { get; init; } = new();

    /// <summary>
    /// Creates a successful execution result
    /// </summary>
    public static StepExecutionResult CreateSuccess(List<FileChange> changes, BuildResult buildOutput, TestValidationResult testResults, StepActionPlan actionPlan, TimeSpan duration, ExecutionMetrics metrics) =>
        new() {
            Success = true,
            Changes = changes,
            BuildResult = buildOutput,
            TestResult = testResults,
            ActionPlan = actionPlan,
            Duration = duration,
            Metrics = metrics
        };

    /// <summary>
    /// Creates a failed execution result
    /// </summary>
    public static StepExecutionResult CreateFailure(string error, List<FileChange>? changes = null, StepActionPlan? actionPlan = null, TimeSpan? duration = null, ExecutionMetrics? metrics = null) =>
        new() {
            Success = false,
            Error = error,
            Changes = changes ?? [],
            ActionPlan = actionPlan,
            Duration = duration ?? TimeSpan.Zero,
            Metrics = metrics ?? new()
        };
}
