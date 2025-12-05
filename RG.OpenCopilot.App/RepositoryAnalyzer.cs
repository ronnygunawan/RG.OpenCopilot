using Octokit;

namespace RG.OpenCopilot.App;

public interface IRepositoryAnalyzer {
    Task<RepositoryAnalysis> AnalyzeAsync(string owner, string repo, CancellationToken cancellationToken = default);
}

public sealed class RepositoryAnalysis {
    public Dictionary<string, long> Languages { get; set; } = new();
    public List<string> KeyFiles { get; set; } = new();
    public string? DetectedTestFramework { get; set; }
    public string? DetectedBuildTool { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class RepositoryAnalyzer : IRepositoryAnalyzer {
    private readonly IGitHubClient _client;
    private readonly ILogger<RepositoryAnalyzer> _logger;

    public RepositoryAnalyzer(IGitHubClient client, ILogger<RepositoryAnalyzer> logger) {
        _client = client;
        _logger = logger;
    }

    public async Task<RepositoryAnalysis> AnalyzeAsync(string owner, string repo, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Analyzing repository {Owner}/{Repo}", owner, repo);

        var analysis = new RepositoryAnalysis();

        try {
            // Get language breakdown
            var languages = await _client.Repository.GetAllLanguages(owner, repo);
            foreach (var lang in languages) {
                analysis.Languages[lang.Name] = lang.NumberOfBytes;
            }

            // Get repository content to find key files
            var contents = await _client.Repository.Content.GetAllContents(owner, repo);
            var keyFiles = new List<string>();

            foreach (var content in contents) {
                if (IsKeyFile(content.Name)) {
                    keyFiles.Add(content.Path);
                }
            }

            // Check for common directories
            try {
                var rootDirs = contents.Where(c => c.Type == ContentType.Dir).Select(c => c.Name).ToList();

                // Check for test directories
                if (rootDirs.Any(d => d.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals("tests", StringComparison.OrdinalIgnoreCase))) {
                    keyFiles.Add("tests/");
                }
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Error checking directories in {Owner}/{Repo}", owner, repo);
            }

            analysis.KeyFiles = keyFiles;
            analysis.DetectedTestFramework = DetectTestFramework(keyFiles, contents);
            analysis.DetectedBuildTool = DetectBuildTool(keyFiles);
            analysis.Summary = GenerateSummary(analysis);

            _logger.LogInformation("Repository analysis complete for {Owner}/{Repo}: {Languages} languages, {KeyFiles} key files",
                owner, repo, analysis.Languages.Count, analysis.KeyFiles.Count);

            return analysis;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error analyzing repository {Owner}/{Repo}", owner, repo);

            // Return minimal analysis on error
            analysis.Summary = "Repository analysis unavailable";
            return analysis;
        }
    }

    private static bool IsKeyFile(string fileName) {
        var knownFiles = new[]
        {
            "package.json", "package-lock.json", "yarn.lock", "pnpm-lock.yaml",
            "tsconfig.json", "webpack.config.js", "vite.config.js",
            "Cargo.toml", "Cargo.lock",
            "go.mod", "go.sum",
            "requirements.txt", "setup.py", "pyproject.toml", "Pipfile",
            "Gemfile", "Gemfile.lock",
            "composer.json", "composer.lock",
            "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle",
            "Makefile", "CMakeLists.txt",
            "README.md", "README.rst", "README.txt",
            "Dockerfile", "docker-compose.yml"
        };

        if (knownFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase)) {
            return true;
        }

        // Check for project files
        if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }

    private static string? DetectTestFramework(List<string> keyFiles, IReadOnlyList<RepositoryContent> contents) {
        // Check for package.json for JS/TS projects
        var packageJson = contents.FirstOrDefault(c => c.Name.Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null) {
            // Note: Detailed framework detection would require fetching and parsing package.json content
            return "JavaScript/TypeScript project (common: Jest, Mocha, Vitest)";
        }

        // Check for .NET projects
        if (keyFiles.Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))) {
            return "xUnit/NUnit/MSTest (detected from .csproj)";
        }

        // Check for Python projects
        if (keyFiles.Any(f => f.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                              f.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase))) {
            return "pytest/unittest (detected from Python project)";
        }

        // Check for Go projects
        if (keyFiles.Any(f => f.Equals("go.mod", StringComparison.OrdinalIgnoreCase))) {
            return "Go testing package (detected from go.mod)";
        }

        // Check for Rust projects
        if (keyFiles.Any(f => f.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))) {
            return "Rust built-in test framework (detected from Cargo.toml)";
        }

        return null;
    }

    private static string? DetectBuildTool(List<string> keyFiles) {
        if (keyFiles.Any(f => f.Equals("package.json", StringComparison.OrdinalIgnoreCase))) {
            if (keyFiles.Any(f => f.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase)))
                return "pnpm";
            if (keyFiles.Any(f => f.Equals("yarn.lock", StringComparison.OrdinalIgnoreCase)))
                return "yarn";
            return "npm";
        }

        if (keyFiles.Any(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                              f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))) {
            return "dotnet";
        }

        if (keyFiles.Any(f => f.Equals("pom.xml", StringComparison.OrdinalIgnoreCase)))
            return "Maven";

        if (keyFiles.Any(f => f.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                              f.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase)))
            return "Gradle";

        if (keyFiles.Any(f => f.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase)))
            return "Cargo";

        if (keyFiles.Any(f => f.Equals("go.mod", StringComparison.OrdinalIgnoreCase)))
            return "Go";

        if (keyFiles.Any(f => f.Equals("Makefile", StringComparison.OrdinalIgnoreCase)))
            return "Make";

        return null;
    }

    private static string GenerateSummary(RepositoryAnalysis analysis) {
        var parts = new List<string>();

        // Languages
        if (analysis.Languages.Any()) {
            var topLanguages = analysis.Languages
                .OrderByDescending(kvp => kvp.Value)
                .Take(3)
                .Select(kvp => kvp.Key);
            parts.Add($"Languages: {string.Join(", ", topLanguages)}");
        }

        // Build tool
        if (!string.IsNullOrEmpty(analysis.DetectedBuildTool)) {
            parts.Add($"Build tool: {analysis.DetectedBuildTool}");
        }

        // Test framework
        if (!string.IsNullOrEmpty(analysis.DetectedTestFramework)) {
            parts.Add($"Testing: {analysis.DetectedTestFramework}");
        }

        // Key files count
        if (analysis.KeyFiles.Any()) {
            var keyFilesDisplay = string.Join(", ", analysis.KeyFiles.Take(5));
            if (analysis.KeyFiles.Count > 5) {
                keyFilesDisplay += $" and {analysis.KeyFiles.Count - 5} more";
            }
            parts.Add($"Key files: {keyFilesDisplay}");
        }

        return string.Join("; ", parts);
    }
}
