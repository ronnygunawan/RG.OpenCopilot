namespace RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;

public sealed class Package {
    public string Name { get; init; } = "";
    public string? Version { get; init; }
    public string Source { get; init; } = "";
    public List<Package> Dependencies { get; init; } = [];
    public PackageManager Manager { get; init; }
}
