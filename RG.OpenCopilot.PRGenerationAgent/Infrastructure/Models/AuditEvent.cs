namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Types of audit events that can be logged
/// </summary>
public enum AuditEventType {
    /// <summary>
    /// Webhook event received from GitHub
    /// </summary>
    WebhookReceived,
    
    /// <summary>
    /// Webhook signature validation performed
    /// </summary>
    WebhookValidation,
    
    /// <summary>
    /// Task state transition occurred
    /// </summary>
    TaskStateTransition,
    
    /// <summary>
    /// GitHub API call made
    /// </summary>
    GitHubApiCall,
    
    /// <summary>
    /// Background job state transition
    /// </summary>
    JobStateTransition,
    
    /// <summary>
    /// Container operation performed
    /// </summary>
    ContainerOperation,
    
    /// <summary>
    /// File operation performed
    /// </summary>
    FileOperation,
    
    /// <summary>
    /// Plan generation started or completed
    /// </summary>
    PlanGeneration,
    
    /// <summary>
    /// Plan execution started or completed
    /// </summary>
    PlanExecution
}

/// <summary>
/// Represents an audit event for observability and compliance
/// </summary>
public sealed class AuditEvent {
    /// <summary>
    /// Type of audit event
    /// </summary>
    public AuditEventType EventType { get; init; }
    
    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Correlation ID to track related events
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// Event description
    /// </summary>
    public string Description { get; init; } = "";
    
    /// <summary>
    /// Additional structured data for the event
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];
    
    /// <summary>
    /// User or system that initiated the event
    /// </summary>
    public string? Initiator { get; init; }
    
    /// <summary>
    /// Target resource affected by the event
    /// </summary>
    public string? Target { get; init; }
    
    /// <summary>
    /// Result of the operation (Success, Failure, etc.)
    /// </summary>
    public string? Result { get; init; }
    
    /// <summary>
    /// Duration of the operation in milliseconds
    /// </summary>
    public long? DurationMs { get; init; }
    
    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}
