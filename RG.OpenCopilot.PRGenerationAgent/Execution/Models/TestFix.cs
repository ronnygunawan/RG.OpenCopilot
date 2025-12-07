namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class TestFix {
    public FixTarget Target { get; init; }
    public string FilePath { get; init; } = "";
    public string Description { get; init; } = "";
    public string OriginalCode { get; init; } = "";
    public string FixedCode { get; init; } = "";
}
