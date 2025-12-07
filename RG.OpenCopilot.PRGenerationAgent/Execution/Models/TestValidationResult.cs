namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class TestValidationResult {
    public bool AllPassed { get; init; }
    public int TotalTests { get; init; }
    public int PassedTests { get; init; }
    public int FailedTests { get; init; }
    public int SkippedTests { get; init; }
    public int Attempts { get; init; }
    public List<TestFailure> RemainingFailures { get; init; } = [];
    public List<TestFix> FixesApplied { get; init; } = [];
    public TimeSpan Duration { get; init; }
    public string Summary { get; init; } = "";
}
