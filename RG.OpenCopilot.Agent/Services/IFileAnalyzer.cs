using RG.OpenCopilot.Agent.Models;

namespace RG.OpenCopilot.Agent.Services;

/// <summary>
/// Service for analyzing files in containers
/// </summary>
public interface IFileAnalyzer {
    Task<FileStructure> AnalyzeFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    Task<List<string>> ListFilesAsync(string containerId, string pattern, CancellationToken cancellationToken = default);
    Task<FileTree> BuildFileTreeAsync(string containerId, string rootPath = ".", CancellationToken cancellationToken = default);
}
