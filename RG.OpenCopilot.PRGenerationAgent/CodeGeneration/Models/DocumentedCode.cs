namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

/// <summary>
/// Represents code with generated inline documentation
/// </summary>
public sealed class DocumentedCode {
    /// <summary>
    /// The language of the documented code
    /// </summary>
    public string Language { get; init; } = "";

    /// <summary>
    /// The original code content
    /// </summary>
    public string OriginalCode { get; init; } = "";

    /// <summary>
    /// The code with generated documentation
    /// </summary>
    public string DocumentedCodeContent { get; init; } = "";

    /// <summary>
    /// Number of documentation comments added
    /// </summary>
    public int DocumentationCount { get; init; }
}
