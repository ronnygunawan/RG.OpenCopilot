namespace RG.OpenCopilot.Agent.Execution.Models;

/// <summary>
/// Represents a single code modification action
/// </summary>
public sealed class CodeAction {
    public ActionType Type { get; init; }
    public string FilePath { get; init; } = "";
    public string Description { get; init; } = "";
    public CodeGenerationRequest Request { get; init; } = new();
}
