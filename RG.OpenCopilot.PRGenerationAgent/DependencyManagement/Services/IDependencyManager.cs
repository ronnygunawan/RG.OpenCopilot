using RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;

namespace RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Services;

/// <summary>
/// Service for managing package dependencies in containers
/// </summary>
public interface IDependencyManager {
    /// <summary>
    /// Adds a package dependency to the container
    /// </summary>
    Task<DependencyResult> AddDependencyAsync(string containerId, string packageName, string? version = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Recommends packages based on requirements using LLM
    /// </summary>
    Task<List<Package>> RecommendPackagesAsync(string requirement, string language, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detects the package manager used in the container
    /// </summary>
    Task<PackageManager?> DetectPackageManagerAsync(string containerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists installed packages in the container
    /// </summary>
    Task<List<Package>> ListInstalledPackagesAsync(string containerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolves version conflicts between dependencies
    /// </summary>
    Task<ConflictResolution> ResolveVersionConflictsAsync(List<Package> dependencies, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates dependency configuration file with new package
    /// </summary>
    Task UpdateDependencyFileAsync(string containerId, Package package, CancellationToken cancellationToken = default);
}
