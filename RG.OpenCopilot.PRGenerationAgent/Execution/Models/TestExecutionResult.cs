namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class TestExecutionResult {
    public bool Success { get; init; }
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public List<TestFailure> Failures { get; init; } = [];
    public string Output { get; init; } = "";
    public TimeSpan Duration { get; init; }
}
