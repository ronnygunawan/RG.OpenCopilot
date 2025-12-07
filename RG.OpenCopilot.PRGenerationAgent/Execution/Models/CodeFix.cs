namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class CodeFix {
    public string FilePath { get; init; } = "";
    public string Description { get; init; } = "";
    public string OriginalCode { get; init; } = "";
    public string FixedCode { get; init; } = "";
    public FixConfidence Confidence { get; init; }
}
