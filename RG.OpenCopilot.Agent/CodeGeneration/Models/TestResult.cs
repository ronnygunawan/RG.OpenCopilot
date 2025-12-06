namespace RG.OpenCopilot.Agent.CodeGeneration.Models;

public sealed class TestResult {
    public bool Success { get; init; }
    public int TotalTests { get; init; }
    public int PassedTests { get; init; }
    public int FailedTests { get; init; }
    public List<string> Failures { get; init; } = [];
    public string Output { get; init; } = "";
}
