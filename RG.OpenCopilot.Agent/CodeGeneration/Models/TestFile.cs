namespace RG.OpenCopilot.Agent.CodeGeneration.Models;

public sealed class TestFile {
    public string Path { get; init; } = "";
    public string Content { get; init; } = "";
    public string Framework { get; init; } = "";
}
