namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Services;

/// <summary>
/// Provides directory management operations for container file systems
/// </summary>
public interface IDirectoryOperations {
    /// <summary>
    /// Creates a directory in the container with parent directory support
    /// </summary>
    Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a directory exists in the container
    /// </summary>
    Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a file or directory from source to destination
    /// </summary>
    Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a file or directory from source to destination
    /// </summary>
    Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file or directory at the specified path
    /// </summary>
    Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the contents of a directory
    /// </summary>
    Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default);
}
