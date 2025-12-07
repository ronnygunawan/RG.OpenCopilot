namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class FormatResult {
    public bool Success { get; init; }
    public int FilesFormatted { get; init; }
    public List<string> FormattedFiles { get; init; } = [];
}
