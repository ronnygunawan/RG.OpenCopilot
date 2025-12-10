namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Interface for audit logging service
/// </summary>
public interface IAuditLogger {
    /// <summary>
    /// Log an audit event
    /// </summary>
    /// <param name="auditEvent">The audit event to log</param>
    void LogAuditEvent(AuditEvent auditEvent);
    
    /// <summary>
    /// Log a webhook received event
    /// </summary>
    /// <param name="eventType">GitHub event type</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="data">Additional event data</param>
    void LogWebhookReceived(string eventType, string? correlationId, Dictionary<string, object>? data = null);
    
    /// <summary>
    /// Log a webhook validation event
    /// </summary>
    /// <param name="isValid">Whether validation succeeded</param>
    /// <param name="correlationId">Correlation ID</param>
    void LogWebhookValidation(bool isValid, string? correlationId);
    
    /// <summary>
    /// Log a task state transition
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="fromState">Previous state</param>
    /// <param name="toState">New state</param>
    /// <param name="correlationId">Correlation ID</param>
    void LogTaskStateTransition(string taskId, string fromState, string toState, string? correlationId);
    
    /// <summary>
    /// Log a GitHub API call
    /// </summary>
    /// <param name="operation">API operation name</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the call succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogGitHubApiCall(string operation, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a job state transition
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <param name="fromState">Previous state</param>
    /// <param name="toState">New state</param>
    /// <param name="correlationId">Correlation ID</param>
    void LogJobStateTransition(string jobId, string fromState, string toState, string? correlationId);
    
    /// <summary>
    /// Log a container operation
    /// </summary>
    /// <param name="operation">Container operation name</param>
    /// <param name="containerId">Container identifier</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogContainerOperation(string operation, string? containerId, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a file operation
    /// </summary>
    /// <param name="operation">File operation name</param>
    /// <param name="filePath">File path</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogFileOperation(string operation, string filePath, string? correlationId, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a plan generation event
    /// </summary>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the generation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogPlanGeneration(int issueNumber, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a plan execution event
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="correlationId">Correlation ID</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the execution succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogPlanExecution(string taskId, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null);
}
