# FileEditor Documentation

## Overview

The `FileEditor` service provides file creation, modification, and deletion capabilities within Docker containers with comprehensive change tracking. It's designed to work with the Container Executor Service to manage files during code generation tasks while maintaining a complete audit trail of all changes for commit message generation.

## Purpose

- **Create new files** in containers with automatic directory creation
- **Modify existing files** using transformation functions
- **Delete files** with safety checks for critical files
- **Track all changes** for intelligent commit message generation
- **Ensure atomicity** by tracking operations before committing

## Architecture

The FileEditor is part of the container-based executor architecture:

1. **IFileEditor Interface**: Defined in `RG.OpenCopilot.PRGenerationAgent/FileOperations/Services/IFileEditor.cs`
2. **FileEditor Implementation**: In `RG.OpenCopilot.PRGenerationAgent.Services/Docker/FileEditor.cs`
3. **Change Tracking**: Uses `FileChange` and `ChangeType` models
4. **Container Integration**: Works with `IContainerManager` for file operations

## Components

### 1. IFileEditor Interface

```csharp
public interface IFileEditor {
    Task CreateFileAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default);
    Task ModifyFileAsync(string containerId, string filePath, Func<string, string> transform, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    List<FileChange> GetChanges();
    void ClearChanges();
}
```

### 2. FileChange Model

Tracks individual file operations:

```csharp
public sealed class FileChange {
    public ChangeType Type { get; init; }      // Created, Modified, or Deleted
    public string Path { get; init; }          // File path
    public string? OldContent { get; init; }   // Original content (null for Created)
    public string? NewContent { get; init; }   // New content (null for Deleted)
}
```

### 3. ChangeType Enum

```csharp
public enum ChangeType {
    Created,
    Modified,
    Deleted
}
```

## Usage Examples

### Basic File Creation

```csharp
var fileEditor = serviceProvider.GetRequiredService<IFileEditor>();

await fileEditor.CreateFileAsync(
    containerId: "abc123",
    filePath: "src/Services/NewService.cs",
    content: """
        namespace MyApp.Services;
        
        public class NewService {
            public void DoWork() { }
        }
        """
);

// The parent directory "src/Services/" is created automatically if it doesn't exist
```

### Modifying Existing Files

```csharp
// Add a new method to an existing class
await fileEditor.ModifyFileAsync(
    containerId: "abc123",
    filePath: "src/Services/ExistingService.cs",
    transform: content => {
        // Add a new method before the closing brace
        var closingBrace = content.LastIndexOf('}');
        var newMethod = """
            
                public void NewMethod() {
                    // Implementation
                }
            """;
        return content.Insert(closingBrace, newMethod);
    }
);
```

### Deleting Files (with Safety Checks)

```csharp
// Delete a temporary file
await fileEditor.DeleteFileAsync(
    containerId: "abc123",
    filePath: "temp/backup.txt"
);

// This will throw InvalidOperationException:
// await fileEditor.DeleteFileAsync(containerId, "README.md");  // Critical file
// await fileEditor.DeleteFileAsync(containerId, ".git/config"); // Git directory
```

### Tracking Changes for Commit Messages

```csharp
// Perform multiple operations
await fileEditor.CreateFileAsync(containerId, "NewFile.cs", "content");
await fileEditor.ModifyFileAsync(containerId, "OldFile.cs", c => c + "\n// Updated");
await fileEditor.DeleteFileAsync(containerId, "ObsoleteFile.cs");

// Get all tracked changes
var changes = fileEditor.GetChanges();
// Returns 3 FileChange objects

// Generate commit message based on changes
var commitMessage = GenerateCommitMessage(changes);
// Example: "Add NewFile.cs, modify OldFile.cs, remove ObsoleteFile.cs"

// Clear changes after commit
fileEditor.ClearChanges();
```

### Complete Workflow Example

```csharp
public async Task ExecuteCodeGeneration(string containerId) {
    var fileEditor = serviceProvider.GetRequiredService<IFileEditor>();
    
    try {
        // 1. Create new test file
        await fileEditor.CreateFileAsync(
            containerId,
            "tests/MyFeatureTests.cs",
            GenerateTestContent()
        );
        
        // 2. Modify existing implementation
        await fileEditor.ModifyFileAsync(
            containerId,
            "src/MyFeature.cs",
            content => AddImplementation(content)
        );
        
        // 3. Remove obsolete helper file
        await fileEditor.DeleteFileAsync(
            containerId,
            "src/OldHelper.cs"
        );
        
        // 4. Get changes and generate commit message
        var changes = fileEditor.GetChanges();
        var message = FormatCommitMessage(changes);
        
        // 5. Commit changes
        await containerManager.CommitAndPushAsync(
            containerId,
            message,
            owner,
            repo,
            branch,
            token
        );
        
        // 6. Clear tracking for next operation
        fileEditor.ClearChanges();
    }
    catch (Exception ex) {
        _logger.LogError(ex, "Code generation failed");
        throw;
    }
}
```

## Features

### 1. Automatic Directory Creation

When creating files, the FileEditor automatically creates parent directories:

```csharp
await fileEditor.CreateFileAsync(
    containerId,
    "deeply/nested/directory/file.txt",
    "content"
);
// Creates all directories: deeply/, deeply/nested/, deeply/nested/directory/
```

### 2. Safety Checks

The FileEditor prevents deletion of critical files:

- **Git files**: `.git/*`, `.gitignore`
- **License files**: `LICENSE`, `LICENSE.txt`, `LICENSE.md`
- **Documentation**: `README.md`
- **Docker files**: `.dockerignore`

```csharp
// This throws InvalidOperationException
await fileEditor.DeleteFileAsync(containerId, "README.md");
```

### 3. Change Detection

The FileEditor skips writes when content is unchanged:

```csharp
await fileEditor.ModifyFileAsync(
    containerId,
    "file.txt",
    transform: content => content  // No change
);
// No write operation is performed, no change is tracked
```

### 4. Change Tracking

All operations are tracked with complete before/after content:

```csharp
var changes = fileEditor.GetChanges();
foreach (var change in changes) {
    switch (change.Type) {
        case ChangeType.Created:
            Console.WriteLine($"Created: {change.Path} ({change.NewContent.Length} bytes)");
            break;
        case ChangeType.Modified:
            Console.WriteLine($"Modified: {change.Path}");
            Console.WriteLine($"  Old: {change.OldContent.Length} bytes");
            Console.WriteLine($"  New: {change.NewContent.Length} bytes");
            break;
        case ChangeType.Deleted:
            Console.WriteLine($"Deleted: {change.Path} ({change.OldContent.Length} bytes)");
            break;
    }
}
```

## Error Handling

### CreateFileAsync

- Throws `InvalidOperationException` if file already exists
- Throws `InvalidOperationException` if directory creation fails
- Throws `InvalidOperationException` if write operation fails

### ModifyFileAsync

- Throws `InvalidOperationException` if file doesn't exist
- Throws `InvalidOperationException` if read operation fails
- Throws `InvalidOperationException` if write operation fails

### DeleteFileAsync

- Throws `InvalidOperationException` if attempting to delete critical files
- Throws `InvalidOperationException` if attempting to delete files in `.git` directory
- Silently returns if file doesn't exist (idempotent)

## Best Practices

### 1. Clear Changes After Commits

```csharp
// Make changes
await fileEditor.CreateFileAsync(containerId, "file.txt", "content");

// Commit
await containerManager.CommitAndPushAsync(...);

// Clear tracking for next batch of changes
fileEditor.ClearChanges();
```

### 2. Use Transformation Functions for Safe Modifications

```csharp
// Good: Pure transformation function
await fileEditor.ModifyFileAsync(
    containerId,
    "file.cs",
    transform: content => content.Replace("OldClass", "NewClass")
);

// Bad: Side effects in transformation
await fileEditor.ModifyFileAsync(
    containerId,
    "file.cs",
    transform: content => {
        _someState = true;  // Don't do this
        return content;
    }
);
```

### 3. Check for Existing Files Before Creating

```csharp
// Use CreateFileAsync for new files
await fileEditor.CreateFileAsync(containerId, "new-file.txt", "content");

// Use ModifyFileAsync for existing files
await fileEditor.ModifyFileAsync(containerId, "existing-file.txt", c => c + "\nappended");
```

### 4. Generate Descriptive Commit Messages

```csharp
string GenerateCommitMessage(List<FileChange> changes) {
    var created = changes.Where(c => c.Type == ChangeType.Created).ToList();
    var modified = changes.Where(c => c.Type == ChangeType.Modified).ToList();
    var deleted = changes.Where(c => c.Type == ChangeType.Deleted).ToList();
    
    var parts = new List<string>();
    
    if (created.Any()) {
        parts.Add($"Add {string.Join(", ", created.Select(c => Path.GetFileName(c.Path)))}");
    }
    if (modified.Any()) {
        parts.Add($"Update {string.Join(", ", modified.Select(c => Path.GetFileName(c.Path)))}");
    }
    if (deleted.Any()) {
        parts.Add($"Remove {string.Join(", ", deleted.Select(c => Path.GetFileName(c.Path)))}");
    }
    
    return string.Join("; ", parts);
}
```

## Integration with Container Executor

The FileEditor is designed to work seamlessly with the Container Executor Service:

```csharp
public class ContainerExecutorService : IExecutorService {
    private readonly IFileEditor _fileEditor;
    private readonly IContainerManager _containerManager;
    
    public async Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken) {
        var containerId = await _containerManager.CreateContainerAsync(...);
        
        try {
            // Use FileEditor to make changes
            await _fileEditor.CreateFileAsync(containerId, "NewFile.cs", content);
            await _fileEditor.ModifyFileAsync(containerId, "Existing.cs", transform);
            
            // Generate smart commit message from tracked changes
            var changes = _fileEditor.GetChanges();
            var commitMessage = FormatCommitMessage(changes);
            
            // Commit with descriptive message
            await _containerManager.CommitAndPushAsync(containerId, commitMessage, ...);
            
            // Clear for next iteration
            _fileEditor.ClearChanges();
        }
        finally {
            await _containerManager.CleanupContainerAsync(containerId);
        }
    }
}
```

## Service Registration

The FileEditor is registered as a singleton in `Program.cs`:

```csharp
builder.Services.AddSingleton<IFileEditor, FileEditor>();
```

## Testing

### Unit Tests

The FileEditor includes 17 comprehensive unit tests covering:
- File creation with directory creation
- File modification with transformation functions
- File deletion with safety checks
- Change tracking and clearing
- Error conditions and edge cases

Run unit tests:
```bash
dotnet test --filter "FullyQualifiedName~FileEditorTests"
```

### Integration Tests

5 integration tests are provided that require Docker:
- Creating files in real containers
- Modifying files with actual read/write operations
- Deleting files with verification
- Complete workflows with multiple operations
- Nested directory creation

Run integration tests (requires Docker):
```bash
dotnet test --filter "FullyQualifiedName~FileEditorIntegrationTests"
```

## Dependencies

- `IContainerManager`: For executing container commands and file operations
- `ILogger<FileEditor>`: For logging operations and errors

## Thread Safety

The FileEditor is **not thread-safe** by design. Each instance should be used for a single container execution workflow. The service is registered as a singleton, but you should use dependency injection to get a fresh instance for each execution context, or implement thread-safe change tracking if concurrent access is required.

## Performance Considerations

- **File reads**: Each modification reads the entire file into memory
- **Change tracking**: All changes are kept in memory until cleared
- **Large files**: Be cautious with large files as content is stored in change tracking
- **Batch operations**: For many files, consider batching commits to reduce overhead

## Future Enhancements

Potential improvements for the FileEditor:
- Diff-based change tracking (store only deltas, not full content)
- Streaming support for large files
- Atomic multi-file operations with rollback
- Validation hooks (linting, formatting) before writes
- File watch notifications for external changes
- Binary file support with base64 encoding
