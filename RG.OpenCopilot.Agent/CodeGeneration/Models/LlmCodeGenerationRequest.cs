namespace RG.OpenCopilot.Agent.CodeGeneration.Models;

/// <summary>
/// Request for LLM-driven code generation with full context
/// </summary>
public sealed class LlmCodeGenerationRequest {
    public string Description { get; init; } = "";
    public string Language { get; init; } = "";
    public string FilePath { get; init; } = "";
    public List<string> Dependencies { get; init; } = [];
    public Dictionary<string, string> Context { get; init; } = [];
}
