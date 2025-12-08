namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Status of a background job
/// </summary>
public enum BackgroundJobStatus {
    /// <summary>
    /// Job is queued and waiting to be processed
    /// </summary>
    Queued,
    
    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing,
    
    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Job failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Job was cancelled
    /// </summary>
    Cancelled
}

/// <summary>
/// Detailed status information for a background job
/// </summary>
public sealed class BackgroundJobStatusInfo {
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; init; } = "";
    
    /// <summary>
    /// Job type
    /// </summary>
    public string JobType { get; init; } = "";
    
    /// <summary>
    /// Current status
    /// </summary>
    public BackgroundJobStatus Status { get; init; }
    
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// When the job started processing
    /// </summary>
    public DateTime? StartedAt { get; init; }
    
    /// <summary>
    /// When the job completed (successfully or failed)
    /// </summary>
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Job metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
    
    /// <summary>
    /// Result data from the job (JSON serialized)
    /// </summary>
    public string? ResultData { get; init; }
}
