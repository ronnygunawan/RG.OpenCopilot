namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class TestFailure {
    public string TestName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
    public string? StackTrace { get; init; }
    public FailureType Type { get; init; }
    public string? ExpectedValue { get; init; }
    public string? ActualValue { get; init; }
}
