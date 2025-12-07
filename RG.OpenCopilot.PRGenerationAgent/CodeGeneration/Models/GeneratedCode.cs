namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class GeneratedCode {
    public string Code { get; init; } = "";
    public bool SyntaxValid { get; init; }
    public List<string> Warnings { get; init; } = [];
    public string Language { get; init; } = "";
}
