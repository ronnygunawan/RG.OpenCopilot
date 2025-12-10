namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Persisted audit log entry
/// </summary>
public sealed class AuditLog {
    /// <summary>
    /// Unique identifier for the audit log entry
    /// </summary>
    public string Id { get; init; } = "";
    
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
    /// Additional structured data for the event (stored as JSON)
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
