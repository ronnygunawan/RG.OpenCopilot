namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

/// <summary>
/// Request for code generation with specific requirements
/// </summary>
public sealed class CodeGenerationRequest {
    public string Content { get; init; } = "";
    public string? BeforeMarker { get; init; }
    public string? AfterMarker { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = [];
}
