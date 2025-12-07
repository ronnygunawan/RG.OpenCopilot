namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

public sealed class BuildToolsStatus {
    public bool DotnetAvailable { get; init; }
    public bool NpmAvailable { get; init; }
    public bool GradleAvailable { get; init; }
    public bool MavenAvailable { get; init; }
    public bool GoAvailable { get; init; }
    public bool CargoAvailable { get; init; }
    public List<string> MissingTools { get; init; } = [];
}
