using RG.OpenCopilot.Agent.Models;

namespace RG.OpenCopilot.Agent.Services;

/// <summary>
/// Service for creating, modifying, and deleting files in containers with change tracking
/// </summary>
public interface IFileEditor {
    Task CreateFileAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default);
    Task ModifyFileAsync(string containerId, string filePath, Func<string, string> transform, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    List<FileChange> GetChanges();
    void ClearChanges();
}
