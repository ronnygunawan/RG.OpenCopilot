namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

/// <summary>
/// Represents generated API documentation
/// </summary>
public sealed class ApiDocumentation {
    /// <summary>
    /// The format of the documentation
    /// </summary>
    public ApiDocFormat Format { get; init; }

    /// <summary>
    /// The generated documentation content
    /// </summary>
    public string Content { get; init; } = "";

    /// <summary>
    /// File path where documentation should be saved
    /// </summary>
    public string FilePath { get; init; } = "";
}
