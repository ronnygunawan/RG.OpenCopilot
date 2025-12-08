namespace RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;

public sealed class DependencyResult {
    public bool Success { get; init; }
    public Package? InstalledPackage { get; init; }
    public List<Package> AdditionalDependencies { get; init; } = [];
    public List<VersionConflict> Conflicts { get; init; } = [];
    public string? Error { get; init; }
}
