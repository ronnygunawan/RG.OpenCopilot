using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;

namespace RG.OpenCopilot.PRGenerationAgent.Services.DependencyManagement;

internal sealed class DependencyManager : IDependencyManager {
    private readonly IContainerManager _containerManager;
    private readonly Kernel _kernel;
    private readonly ILogger<DependencyManager> _logger;

    public DependencyManager(
        IContainerManager containerManager,
        Kernel kernel,
        ILogger<DependencyManager> logger) {
        _containerManager = containerManager;
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<DependencyResult> AddDependencyAsync(
        string containerId,
        string packageName,
        string? version = null,
        CancellationToken cancellationToken = default) {
        try {
            _logger.LogInformation("Adding dependency {PackageName} version {Version} to container {ContainerId}",
                packageName, version ?? "latest", containerId);

            // Detect package manager
            var packageManager = await DetectPackageManagerAsync(containerId, cancellationToken);
            if (packageManager == null) {
                return new DependencyResult {
                    Success = false,
                    Error = "Could not detect package manager in container"
                };
            }

            _logger.LogInformation("Detected package manager: {PackageManager}", packageManager);

            // Install the package
            var installResult = await InstallPackageAsync(
                containerId,
                packageManager.Value,
                packageName,
                version,
                cancellationToken);

            if (!installResult.Success) {
                return new DependencyResult {
                    Success = false,
                    Error = $"Failed to install package: {installResult.Error}"
                };
            }

            // Create package object
            var package = new Package {
                Name = packageName,
                Version = version,
                Manager = packageManager.Value,
                Source = GetDefaultSource(packageManager.Value)
            };

            // Update dependency file
            await UpdateDependencyFileAsync(containerId, package, cancellationToken);

            return new DependencyResult {
                Success = true,
                InstalledPackage = package
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error adding dependency {PackageName}", packageName);
            return new DependencyResult {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<List<Package>> RecommendPackagesAsync(
        string requirement,
        string language,
        CancellationToken cancellationToken = default) {
        var prompt = $$"""
            You are a package dependency expert. Given a requirement and programming language,
            recommend appropriate packages from the respective package registry.
            
            Requirement: {{requirement}}
            Language: {{language}}
            
            Provide recommendations as a JSON array with this format:
            [
              {
                "name": "package-name",
                "version": "recommended-version",
                "reason": "why this package is recommended"
              }
            ]
            
            Only recommend well-maintained, popular packages. Limit to 5 recommendations.
            Return only the JSON array, no additional text.
            """;

        try {
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            var response = result.ToString();
            
            _logger.LogInformation("LLM package recommendations for {Language}: {Response}",
                language, response);

            // Parse the response and convert to Package objects
            var packages = ParsePackageRecommendations(response, language);
            return packages;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error getting package recommendations");
            return [];
        }
    }

    public async Task<PackageManager?> DetectPackageManagerAsync(
        string containerId,
        CancellationToken cancellationToken = default) {
        // Check for various dependency files in order of precedence
        var detectionChecks = new[] {
            ("*.csproj", PackageManager.NuGet),
            ("packages.config", PackageManager.NuGet),
            ("package.json", PackageManager.Npm),
            ("requirements.txt", PackageManager.Pip),
            ("Pipfile", PackageManager.Pip),
            ("pom.xml", PackageManager.Maven),
            ("build.gradle", PackageManager.Gradle),
            ("build.gradle.kts", PackageManager.Gradle),
            ("Cargo.toml", PackageManager.Cargo),
            ("go.mod", PackageManager.GoModules),
            ("composer.json", PackageManager.Composer),
            ("Gemfile", PackageManager.RubyGems)
        };

        foreach (var (pattern, manager) in detectionChecks) {
            var result = await _containerManager.ExecuteInContainerAsync(
                containerId,
                "sh",
                new[] { "-c", $"find /workspace -name '{pattern}' -type f | head -1" },
                cancellationToken);

            if (result.Success && !string.IsNullOrWhiteSpace(result.Output)) {
                _logger.LogInformation("Detected {PackageManager} via {Pattern}", manager, pattern);
                return manager;
            }
        }

        _logger.LogWarning("Could not detect package manager in container {ContainerId}", containerId);
        return null;
    }

    public async Task<List<Package>> ListInstalledPackagesAsync(
        string containerId,
        CancellationToken cancellationToken = default) {
        var packageManager = await DetectPackageManagerAsync(containerId, cancellationToken);
        if (packageManager == null) {
            return [];
        }

        var (command, args) = GetListPackagesCommand(packageManager.Value);
        var result = await _containerManager.ExecuteInContainerAsync(
            containerId,
            command,
            args,
            cancellationToken);

        if (!result.Success) {
            _logger.LogWarning("Failed to list installed packages: {Error}", result.Error);
            return [];
        }

        return ParseInstalledPackages(result.Output, packageManager.Value);
    }

    public async Task<ConflictResolution> ResolveVersionConflictsAsync(
        List<Package> dependencies,
        CancellationToken cancellationToken = default) {
        // Group packages by name
        var packageGroups = dependencies.GroupBy(p => p.Name).ToList();
        
        var conflicts = new List<VersionConflict>();
        var resolved = new List<Package>();

        foreach (var group in packageGroups) {
            var packages = group.ToList();
            if (packages.Count == 1) {
                resolved.Add(packages[0]);
                continue;
            }

            // Multiple versions of the same package - need to resolve
            var versions = packages.Where(p => p.Version != null).Select(p => p.Version!).Distinct().ToList();
            
            if (versions.Count == 0) {
                // All have null version
                resolved.Add(packages[0]);
            }
            else if (versions.Count == 1) {
                // All non-null packages have same version
                resolved.Add(packages.First(p => p.Version != null));
            }
            else {
                // Version conflict detected
                var latestVersion = GetLatestVersion(versions);
                
                foreach (var pkg in packages.Where(p => p.Version != latestVersion)) {
                    conflicts.Add(new VersionConflict {
                        PackageName = pkg.Name,
                        RequestedVersion = pkg.Version ?? "latest",
                        InstalledVersion = latestVersion,
                        Severity = ConflictSeverity.Warning
                    });
                }

                resolved.Add(new Package {
                    Name = group.Key,
                    Version = latestVersion,
                    Manager = packages[0].Manager,
                    Source = packages[0].Source
                });
            }
        }

        return new ConflictResolution {
            Success = conflicts.Count == 0 || conflicts.All(c => c.Severity != ConflictSeverity.Error),
            ResolvedPackages = resolved,
            RemainingConflicts = conflicts
        };
    }

    public async Task UpdateDependencyFileAsync(
        string containerId,
        Package package,
        CancellationToken cancellationToken = default) {
        var (filePath, updateCommand) = GetUpdateDependencyFileCommand(package);
        
        if (updateCommand != null) {
            var result = await _containerManager.ExecuteInContainerAsync(
                containerId,
                "sh",
                new[] { "-c", updateCommand },
                cancellationToken);

            if (!result.Success) {
                _logger.LogWarning("Failed to update dependency file: {Error}", result.Error);
            }
        }
    }

    private async Task<CommandResult> InstallPackageAsync(
        string containerId,
        PackageManager manager,
        string packageName,
        string? version,
        CancellationToken cancellationToken) {
        var (command, args) = GetInstallCommand(manager, packageName, version);
        
        return await _containerManager.ExecuteInContainerAsync(
            containerId,
            command,
            args,
            cancellationToken);
    }

    private static (string command, string[] args) GetInstallCommand(
        PackageManager manager,
        string packageName,
        string? version) {
        var packageSpec = version != null ? $"{packageName}@{version}" : packageName;
        
        return manager switch {
            PackageManager.NuGet => ("dotnet", new[] { "add", "package", packageName, version != null ? "--version" : "", version ?? "" }.Where(s => !string.IsNullOrEmpty(s)).ToArray()),
            PackageManager.Npm => ("npm", new[] { "install", version != null ? $"{packageName}@{version}" : packageName }),
            PackageManager.Pip => ("pip", new[] { "install", version != null ? $"{packageName}=={version}" : packageName }),
            PackageManager.Maven => ("mvn", new[] { "dependency:get", $"-Dartifact={packageName}:{version ?? "LATEST"}" }),
            PackageManager.Gradle => ("gradle", new[] { "dependencies", "--write-locks" }),
            PackageManager.Cargo => ("cargo", new[] { "add", packageName, version != null ? $"--vers {version}" : "" }.Where(s => !string.IsNullOrEmpty(s)).ToArray()),
            PackageManager.GoModules => ("go", new[] { "get", version != null ? $"{packageName}@{version}" : packageName }),
            PackageManager.Composer => ("composer", new[] { "require", version != null ? $"{packageName}:{version}" : packageName }),
            PackageManager.RubyGems => ("bundle", new[] { "add", packageName, version != null ? $"--version={version}" : "" }.Where(s => !string.IsNullOrEmpty(s)).ToArray()),
            _ => throw new NotSupportedException($"Package manager {manager} not supported")
        };
    }

    private static (string command, string[] args) GetListPackagesCommand(PackageManager manager) {
        return manager switch {
            PackageManager.NuGet => ("dotnet", new[] { "list", "package" }),
            PackageManager.Npm => ("npm", new[] { "list", "--depth=0", "--json" }),
            PackageManager.Pip => ("pip", new[] { "list", "--format=json" }),
            PackageManager.Maven => ("mvn", new[] { "dependency:list" }),
            PackageManager.Gradle => ("gradle", new[] { "dependencies" }),
            PackageManager.Cargo => ("cargo", new[] { "tree", "--depth=0" }),
            PackageManager.GoModules => ("go", new[] { "list", "-m", "all" }),
            PackageManager.Composer => ("composer", new[] { "show", "--direct" }),
            PackageManager.RubyGems => ("bundle", new[] { "list" }),
            _ => throw new NotSupportedException($"Package manager {manager} not supported")
        };
    }

    private static (string? filePath, string? command) GetUpdateDependencyFileCommand(Package package) {
        return package.Manager switch {
            PackageManager.NuGet => (null, null), // dotnet add package already updates .csproj
            PackageManager.Npm => ("package.json", null), // npm install already updates package.json
            PackageManager.Pip => ("requirements.txt", package.Version != null 
                ? $"echo '{package.Name}=={package.Version}' >> /workspace/requirements.txt"
                : $"echo '{package.Name}' >> /workspace/requirements.txt"),
            PackageManager.Maven => ("pom.xml", null), // Maven uses pom.xml editing
            PackageManager.Gradle => ("build.gradle", null), // Gradle uses build file editing
            PackageManager.Cargo => (null, null), // cargo add already updates Cargo.toml
            PackageManager.GoModules => (null, null), // go get already updates go.mod
            PackageManager.Composer => (null, null), // composer require already updates composer.json
            PackageManager.RubyGems => (null, null), // bundle add already updates Gemfile
            _ => (null, null)
        };
    }

    private static string GetDefaultSource(PackageManager manager) {
        return manager switch {
            PackageManager.NuGet => "nuget.org",
            PackageManager.Npm => "npmjs.com",
            PackageManager.Pip => "pypi.org",
            PackageManager.Maven => "maven.org",
            PackageManager.Gradle => "maven.org",
            PackageManager.Cargo => "crates.io",
            PackageManager.GoModules => "pkg.go.dev",
            PackageManager.Composer => "packagist.org",
            PackageManager.RubyGems => "rubygems.org",
            _ => "unknown"
        };
    }

    private List<Package> ParsePackageRecommendations(string response, string language) {
        // Simple parsing - in production would use System.Text.Json
        var packages = new List<Package>();
        
        try {
            // Extract JSON array from response
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart) {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                // For now, return empty list - proper JSON parsing would be added in production
                _logger.LogInformation("Parsed package recommendations JSON: {Json}", json);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing package recommendations");
        }

        return packages;
    }

    private static List<Package> ParseInstalledPackages(string output, PackageManager manager) {
        var packages = new List<Package>();
        
        // Simple parsing - each package manager has different output format
        // This would be implemented properly in production
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines) {
            // Skip header lines
            if (line.Contains("Package") || line.Contains("---") || string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            // Basic parsing - would be more sophisticated in production
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) {
                packages.Add(new Package {
                    Name = parts[0].Trim(),
                    Version = parts[1].Trim(),
                    Manager = manager,
                    Source = GetDefaultSource(manager)
                });
            }
        }

        return packages;
    }

    private static string GetLatestVersion(List<string> versions) {
        // Simple version comparison - in production would use proper semver parsing
        // For now, just return the last one alphabetically which often works for semantic versions
        return versions.OrderByDescending(v => v).First();
    }
}
