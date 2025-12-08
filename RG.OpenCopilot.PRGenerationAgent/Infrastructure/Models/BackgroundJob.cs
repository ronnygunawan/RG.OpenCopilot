namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Represents a background job to be processed
/// </summary>
public sealed class BackgroundJob {
    /// <summary>
    /// Unique identifier for the job
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of the job (e.g., "ExecutePlan", "RunTests")
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// JSON serialized payload containing job-specific data
    /// </summary>
    public string Payload { get; init; } = "";

    /// <summary>
    /// Job priority (higher values processed first)
    /// </summary>
    public int Priority { get; init; } = 0;

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the job should be executed (for delayed jobs)
    /// </summary>
    public DateTime? ScheduledFor { get; init; }

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Current retry attempt (0 for first attempt)
    /// </summary>
    public int RetryCount { get; init; } = 0;

    /// <summary>
    /// Cancellation token source for this job
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    /// <summary>
    /// Job metadata (e.g., task ID, repository info)
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}
