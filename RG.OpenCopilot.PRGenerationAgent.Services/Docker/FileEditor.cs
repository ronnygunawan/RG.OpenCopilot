using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Docker;

/// <summary>
/// Manages file operations in Docker containers with change tracking for commit message generation
/// </summary>
public sealed class FileEditor : IFileEditor {
    private readonly IContainerManager _containerManager;
    private readonly ILogger<FileEditor> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly List<FileChange> _changes = [];

    public FileEditor(
        IContainerManager containerManager,
        ILogger<FileEditor> logger,
        IAuditLogger auditLogger) {
        _containerManager = containerManager;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Creates a new file in the container and tracks the change
    /// </summary>
    public async Task CreateFileAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Creating file {FilePath} in container {ContainerId}", filePath, containerId);

        try {
            // Check if file already exists
            var fileExists = await FileExistsAsync(containerId, filePath, cancellationToken);
            if (fileExists) {
                var errorMsg = $"File {filePath} already exists. Use ModifyFileAsync to update existing files.";
                _auditLogger.LogFileOperation(
                    operation: "CreateFile",
                    filePath: filePath,
                    success: false,
                    errorMessage: errorMsg);

                throw new InvalidOperationException(errorMsg);
            }

            // Ensure parent directory exists
            await EnsureDirectoryExistsAsync(containerId, filePath, cancellationToken);

            // Write the file
            await _containerManager.WriteFileInContainerAsync(containerId, filePath, content, cancellationToken);

            // Track the change
            _changes.Add(new FileChange {
                Type = ChangeType.Created,
                Path = filePath,
                OldContent = null,
                NewContent = content
            });

            _logger.LogInformation("File {FilePath} created successfully", filePath);

            _auditLogger.LogFileOperation(
                operation: "CreateFile",
                filePath: filePath,
                success: true);
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            _auditLogger.LogFileOperation(
                operation: "CreateFile",
                filePath: filePath,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Modifies an existing file in the container using a transformation function
    /// </summary>
    public async Task ModifyFileAsync(string containerId, string filePath, Func<string, string> transform, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Modifying file {FilePath} in container {ContainerId}", filePath, containerId);

        try {
            // Read the current content
            string oldContent;
            try {
                oldContent = await _containerManager.ReadFileInContainerAsync(containerId, filePath, cancellationToken);
            }
            catch (InvalidOperationException) {
                var errorMsg = $"File {filePath} does not exist. Use CreateFileAsync to create new files.";
                _auditLogger.LogFileOperation(
                    operation: "ModifyFile",
                    filePath: filePath,
                    success: false,
                    errorMessage: errorMsg);

                throw new InvalidOperationException(errorMsg);
            }

            // Apply the transformation
            var newContent = transform(oldContent);

            // Only write if content has changed
            if (oldContent == newContent) {
                _logger.LogInformation("File {FilePath} content unchanged, skipping write", filePath);

                _auditLogger.LogFileOperation(
                    operation: "ModifyFile",
                    filePath: filePath,
                    success: true);

                return;
            }

            // Write the modified content
            await _containerManager.WriteFileInContainerAsync(containerId, filePath, newContent, cancellationToken);

            // Track the change
            _changes.Add(new FileChange {
                Type = ChangeType.Modified,
                Path = filePath,
                OldContent = oldContent,
                NewContent = newContent
            });

            _logger.LogInformation("File {FilePath} modified successfully", filePath);

            _auditLogger.LogFileOperation(
                operation: "ModifyFile",
                filePath: filePath,
                success: true);
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            _auditLogger.LogFileOperation(
                operation: "ModifyFile",
                filePath: filePath,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Deletes a file from the container with safety checks
    /// </summary>
    public async Task DeleteFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Deleting file {FilePath} in container {ContainerId}", filePath, containerId);

        try {
            // Safety check: don't delete critical files
            if (IsCriticalFile(filePath)) {
                var errorMsg = $"Cannot delete critical file: {filePath}";
                _auditLogger.LogFileOperation(
                    operation: "DeleteFile",
                    filePath: filePath,
                    success: false,
                    errorMessage: errorMsg);

                throw new InvalidOperationException(errorMsg);
            }

            // Read the current content before deletion for tracking
            string oldContent;
            try {
                oldContent = await _containerManager.ReadFileInContainerAsync(containerId, filePath, cancellationToken);
            }
            catch (InvalidOperationException) {
                _logger.LogWarning("File {FilePath} does not exist, skipping deletion", filePath);

                _auditLogger.LogFileOperation(
                    operation: "DeleteFile",
                    filePath: filePath,
                    success: true);

                return;
            }

            // Delete the file
            var result = await _containerManager.ExecuteInContainerAsync(
                containerId: containerId,
                command: "rm",
                args: new[] { "-f", filePath },
                cancellationToken: cancellationToken);

            if (!result.Success) {
                _auditLogger.LogFileOperation(
                    operation: "DeleteFile",
                    filePath: filePath,
                    success: false,
                    errorMessage: result.Error);

                throw new InvalidOperationException($"Failed to delete file {filePath}: {result.Error}");
            }

            // Track the change
            _changes.Add(new FileChange {
                Type = ChangeType.Deleted,
                Path = filePath,
                OldContent = oldContent,
                NewContent = null
            });

            _logger.LogInformation("File {FilePath} deleted successfully", filePath);

            _auditLogger.LogFileOperation(
                operation: "DeleteFile",
                filePath: filePath,
                success: true);
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            _auditLogger.LogFileOperation(
                operation: "DeleteFile",
                filePath: filePath,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets all tracked file changes
    /// </summary>
    public List<FileChange> GetChanges() {
        return new List<FileChange>(_changes);
    }

    /// <summary>
    /// Clears all tracked changes
    /// </summary>
    public void ClearChanges() {
        _changes.Clear();
        _logger.LogDebug("Cleared all tracked file changes");
    }

    private async Task<bool> FileExistsAsync(string containerId, string filePath, CancellationToken cancellationToken) {
        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "test",
            args: new[] { "-f", filePath },
            cancellationToken: cancellationToken);

        return result.Success;
    }

    private async Task EnsureDirectoryExistsAsync(string containerId, string filePath, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory)) {
            return;
        }

        // Normalize path separators to forward slashes for Docker containers (Linux-based)
        var normalizedDirectory = directory.Replace('\\', '/');

        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "mkdir",
            args: new[] { "-p", normalizedDirectory },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to create directory {normalizedDirectory}: {result.Error}");
        }
    }

    private static bool IsCriticalFile(string filePath) {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var criticalFiles = new HashSet<string> {
            ".git",
            ".gitignore",
            "license",
            "license.txt",
            "license.md",
            "readme.md",
            ".dockerignore"
        };

        // Don't allow deletion of files in .git directory
        var normalizedPath = filePath.Replace('\\', '/');
        var pathComponents = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathComponents.Any(component => component == ".git")) {
            return true;
        }

        return criticalFiles.Contains(fileName);
    }
}
