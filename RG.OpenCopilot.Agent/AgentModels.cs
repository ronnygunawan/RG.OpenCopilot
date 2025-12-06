namespace RG.OpenCopilot.Agent;

public sealed class AgentPlan {
    public string ProblemSummary { get; init; } = "";
    public List<string> Constraints { get; init; } = [];
    public List<PlanStep> Steps { get; init; } = [];
    public List<string> Checklist { get; init; } = [];
    public List<string> FileTargets { get; init; } = [];
}

public sealed class PlanStep {
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Details { get; init; } = "";
    public bool Done { get; set; }
}

public enum AgentTaskStatus {
    PendingPlanning,
    Planned,
    Executing,
    Completed,
    Blocked,
    Failed
}

public sealed class AgentTask {
    public string Id { get; init; } = "";
    public long InstallationId { get; init; }
    public string RepositoryOwner { get; init; } = "";
    public string RepositoryName { get; init; } = "";
    public int IssueNumber { get; init; }
    public AgentPlan? Plan { get; set; }
    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.PendingPlanning;
}

public sealed class AgentTaskContext {
    public string IssueTitle { get; init; } = "";
    public string IssueBody { get; init; } = "";
    public string? InstructionsMarkdown { get; init; }
    public string? RepositorySummary { get; init; }
}

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the structure of a file including its classes, functions, and other elements
/// </summary>
public sealed class FileStructure {
    public string FilePath { get; init; } = "";
    public string Language { get; init; } = "";
    public List<string> Imports { get; init; } = [];
    public List<string> Classes { get; init; } = [];
    public List<string> Functions { get; init; } = [];
    public List<string> Namespaces { get; init; } = [];
}

/// <summary>
/// Represents a node in the file tree
/// </summary>
public sealed class FileTreeNode {
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsDirectory { get; init; }
    public List<FileTreeNode> Children { get; init; } = [];
}

/// <summary>
/// Represents the file tree structure of a repository
/// </summary>
public sealed class FileTree {
    public FileTreeNode Root { get; init; } = new();
    public List<string> AllFiles { get; init; } = [];
}

/// <summary>
/// Service for analyzing files in containers
/// </summary>
public interface IFileAnalyzer {
    Task<FileStructure> AnalyzeFileAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    Task<List<string>> ListFilesAsync(string containerId, string pattern, CancellationToken cancellationToken = default);
    Task<FileTree> BuildFileTreeAsync(string containerId, string rootPath = ".", CancellationToken cancellationToken = default);
}

/// <summary>
/// Type of file change operation
/// </summary>
public enum ChangeType {
    Created,
    Modified,
    Deleted
}

/// <summary>
/// Represents a tracked file change with old and new content
/// </summary>
public sealed class FileChange {
    public ChangeType Type { get; init; }
    public string Path { get; init; } = "";
    public string? OldContent { get; init; }
    public string? NewContent { get; init; }
}

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
