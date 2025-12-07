using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Docker;

/// <summary>
/// Analyzes files in Docker containers to extract structure and build file trees
/// </summary>
public sealed class FileAnalyzer : IFileAnalyzer {
    private readonly IContainerManager _containerManager;
    private readonly ILogger<FileAnalyzer> _logger;

    public FileAnalyzer(IContainerManager containerManager, ILogger<FileAnalyzer> logger) {
        _containerManager = containerManager;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a file in a container to extract its structure including classes, functions, and imports
    /// </summary>
    /// <param name="containerId">The Docker container ID</param>
    /// <param name="filePath">Path to the file within the container</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A FileStructure containing parsed elements from the file</returns>
    public async Task<FileStructure> AnalyzeFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
        _logger.LogDebug("Analyzing file {FilePath} in container {ContainerId}", filePath, containerId);

        var content = await _containerManager.ReadFileInContainerAsync(containerId, filePath, cancellationToken);
        var language = DetectLanguage(filePath);

        return ParseFile(filePath, content, language);
    }

    /// <summary>
    /// Lists files in a container matching the specified pattern
    /// </summary>
    /// <param name="containerId">The Docker container ID</param>
    /// <param name="pattern">File pattern to match (e.g., "*.cs", "*.js")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of file paths matching the pattern</returns>
    public async Task<List<string>> ListFilesAsync(string containerId, string pattern, CancellationToken cancellationToken = default) {
        _logger.LogDebug("Listing files with pattern {Pattern} in container {ContainerId}", pattern, containerId);

        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-type", "f", "-name", pattern },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            _logger.LogWarning("Failed to list files with pattern {Pattern}: {Error}", pattern, result.Error);
            return new List<string>();
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.TrimStart('.', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    /// <summary>
    /// Builds a hierarchical file tree structure of all files in the container
    /// </summary>
    /// <param name="containerId">The Docker container ID</param>
    /// <param name="rootPath">Root path to start building the tree from (default: ".")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A FileTree containing the hierarchical structure and list of all files</returns>
    public async Task<FileTree> BuildFileTreeAsync(string containerId, string rootPath = ".", CancellationToken cancellationToken = default) {
        _logger.LogDebug("Building file tree for {RootPath} in container {ContainerId}", rootPath, containerId);

        // Get all files using find command
        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { rootPath, "-type", "f" },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            _logger.LogWarning("Failed to build file tree: {Error}", result.Error);
            return new FileTree { Root = new FileTreeNode { Name = rootPath, Path = rootPath, IsDirectory = true } };
        }

        var allFiles = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => path.TrimStart('.', '/'))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        var root = new FileTreeNode {
            Name = rootPath,
            Path = rootPath,
            IsDirectory = true,
            Children = new List<FileTreeNode>()
        };

        BuildTreeStructure(root, allFiles);

        return new FileTree {
            Root = root,
            AllFiles = allFiles
        };
    }

    private void BuildTreeStructure(FileTreeNode root, List<string> files) {
        var filesByDirectory = new Dictionary<string, List<string>>();

        foreach (var file in files) {
            var parts = file.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            var currentPath = "";
            for (int i = 0; i < parts.Length - 1; i++) {
                var parentPath = currentPath;
                currentPath = string.IsNullOrEmpty(currentPath) ? parts[i] : $"{currentPath}/{parts[i]}";

                if (!filesByDirectory.ContainsKey(parentPath)) {
                    filesByDirectory[parentPath] = new List<string>();
                }

                if (!filesByDirectory[parentPath].Contains(currentPath)) {
                    filesByDirectory[parentPath].Add(currentPath);
                }
            }

            var dir = parts.Length > 1 ? string.Join("/", parts.Take(parts.Length - 1)) : "";
            if (!filesByDirectory.ContainsKey(dir)) {
                filesByDirectory[dir] = new List<string>();
            }
            filesByDirectory[dir].Add(file);
        }

        PopulateNode(root, "", filesByDirectory, files);
    }

    private void PopulateNode(FileTreeNode node, string currentPath, Dictionary<string, List<string>> filesByDirectory, List<string> allFiles) {
        if (!filesByDirectory.TryGetValue(currentPath, out var items)) {
            return;
        }

        var childDirectories = new HashSet<string>();
        var childFiles = new HashSet<string>();

        foreach (var item in items) {
            if (allFiles.Contains(item)) {
                childFiles.Add(item);
            } else {
                childDirectories.Add(item);
            }
        }

        foreach (var dir in childDirectories.OrderBy(d => d)) {
            var dirName = dir.Split('/').Last();
            var childNode = new FileTreeNode {
                Name = dirName,
                Path = dir,
                IsDirectory = true,
                Children = new List<FileTreeNode>()
            };
            node.Children.Add(childNode);
            PopulateNode(childNode, dir, filesByDirectory, allFiles);
        }

        foreach (var file in childFiles.OrderBy(f => f)) {
            var fileName = file.Split('/').Last();
            node.Children.Add(new FileTreeNode {
                Name = fileName,
                Path = file,
                IsDirectory = false
            });
        }
    }

    private string DetectLanguage(string filePath) {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".h" or ".hpp" => "c-header",
            _ => "unknown"
        };
    }

    private FileStructure ParseFile(string filePath, string content, string language) {
        var structure = new FileStructure {
            FilePath = filePath,
            Language = language
        };

        switch (language) {
            case "csharp":
                ParseCSharpFile(content, structure);
                break;
            case "javascript":
            case "typescript":
                ParseJavaScriptFile(content, structure);
                break;
            case "python":
                ParsePythonFile(content, structure);
                break;
        }

        return structure;
    }

    private void ParseCSharpFile(string content, FileStructure structure) {
        // Parse namespaces
        var namespaceMatches = Regex.Matches(content, @"namespace\s+([A-Za-z0-9_.]+)");
        foreach (Match match in namespaceMatches) {
            structure.Namespaces.Add(match.Groups[1].Value);
        }

        // Parse using statements
        var usingMatches = Regex.Matches(content, @"using\s+([A-Za-z0-9_.]+)");
        foreach (Match match in usingMatches) {
            structure.Imports.Add(match.Groups[1].Value);
        }

        // Parse classes, interfaces, structs, records
        var classMatches = Regex.Matches(content, @"(?:public|private|internal|protected)?\s*(?:sealed|abstract|static)?\s*(class|interface|struct|record)\s+([A-Za-z0-9_<>]+)");
        foreach (Match match in classMatches) {
            var type = match.Groups[1].Value;
            var name = match.Groups[2].Value;
            structure.Classes.Add($"{type} {name}");
        }

        // Parse methods (simplified - public/private/protected/internal methods)
        var methodMatches = Regex.Matches(content, @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:\w+(?:<[^>]+>)?)\s+([A-Za-z0-9_]+)\s*\(");
        foreach (Match match in methodMatches) {
            var methodName = match.Groups[1].Value;
            if (!IsKeyword(methodName)) {
                structure.Functions.Add(methodName);
            }
        }
    }

    private void ParseJavaScriptFile(string content, FileStructure structure) {
        // Parse imports
        var importMatches = Regex.Matches(content, @"import\s+.*?from\s+['""]([^'""]+)['""]");
        foreach (Match match in importMatches) {
            structure.Imports.Add(match.Groups[1].Value);
        }

        var requireMatches = Regex.Matches(content, @"require\s*\(\s*['""]([^'""]+)['""]\s*\)");
        foreach (Match match in requireMatches) {
            structure.Imports.Add(match.Groups[1].Value);
        }

        // Parse classes
        var classMatches = Regex.Matches(content, @"class\s+([A-Za-z0-9_]+)");
        foreach (Match match in classMatches) {
            structure.Classes.Add(match.Groups[1].Value);
        }

        // Parse functions (function declarations and arrow functions)
        var functionMatches = Regex.Matches(content, @"(?:function\s+([A-Za-z0-9_]+)|(?:const|let|var)\s+([A-Za-z0-9_]+)\s*=\s*(?:async\s*)?\([^)]*\)\s*=>)");
        foreach (Match match in functionMatches) {
            var functionName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(functionName)) {
                structure.Functions.Add(functionName);
            }
        }

        // Parse methods in classes
        var methodMatches = Regex.Matches(content, @"(?:async\s+)?([A-Za-z0-9_]+)\s*\([^)]*\)\s*\{");
        foreach (Match match in methodMatches) {
            var methodName = match.Groups[1].Value;
            if (!IsKeyword(methodName, "javascript") && !structure.Functions.Contains(methodName)) {
                structure.Functions.Add(methodName);
            }
        }
    }

    private void ParsePythonFile(string content, FileStructure structure) {
        // Parse imports
        var importMatches = Regex.Matches(content, @"^(?:from\s+([A-Za-z0-9_.]+)\s+)?import\s+([A-Za-z0-9_., ]+)", RegexOptions.Multiline);
        foreach (Match match in importMatches) {
            if (match.Groups[1].Success) {
                structure.Imports.Add(match.Groups[1].Value);
            }
            var imports = match.Groups[2].Value.Split(',');
            foreach (var imp in imports) {
                var trimmed = imp.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed)) {
                    structure.Imports.Add(trimmed);
                }
            }
        }

        // Parse classes
        var classMatches = Regex.Matches(content, @"^class\s+([A-Za-z0-9_]+)", RegexOptions.Multiline);
        foreach (Match match in classMatches) {
            structure.Classes.Add(match.Groups[1].Value);
        }

        // Parse functions (top-level and methods)
        var functionMatches = Regex.Matches(content, @"^\s*(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\(", RegexOptions.Multiline);
        foreach (Match match in functionMatches) {
            structure.Functions.Add(match.Groups[1].Value);
        }
    }

    private bool IsKeyword(string word, string language = "csharp") {
        var commonKeywords = new HashSet<string> {
            "if", "else", "while", "for", "switch", "case", "return", "new"
        };
        
        if (language == "csharp") {
            var csharpKeywords = new HashSet<string>(commonKeywords) {
                "foreach", "this", "base", "get", "set", "class", "interface", "struct"
            };
            return csharpKeywords.Contains(word.ToLowerInvariant());
        }
        
        if (language == "javascript" || language == "typescript") {
            var jsKeywords = new HashSet<string>(commonKeywords) {
                "function", "class", "const", "let", "var", "async", "await"
            };
            return jsKeywords.Contains(word.ToLowerInvariant());
        }
        
        return commonKeywords.Contains(word.ToLowerInvariant());
    }
}
