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
    /// <param name="data">Additional event data</param>
    void LogWebhookReceived(string eventType, Dictionary<string, object>? data = null);
    
    /// <summary>
    /// Log a webhook validation event
    /// </summary>
    /// <param name="isValid">Whether validation succeeded</param>
    void LogWebhookValidation(bool isValid);
    
    /// <summary>
    /// Log a task state transition
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="fromState">Previous state</param>
    /// <param name="toState">New state</param>
    void LogTaskStateTransition(string taskId, string fromState, string toState);
    
    /// <summary>
    /// Log a GitHub API call
    /// </summary>
    /// <param name="operation">API operation name</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the call succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogGitHubApiCall(string operation, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a job state transition
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <param name="fromState">Previous state</param>
    /// <param name="toState">New state</param>
    void LogJobStateTransition(string jobId, string fromState, string toState);
    
    /// <summary>
    /// Log a container operation
    /// </summary>
    /// <param name="operation">Container operation name</param>
    /// <param name="containerId">Container identifier</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogContainerOperation(string operation, string? containerId, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a file operation
    /// </summary>
    /// <param name="operation">File operation name</param>
    /// <param name="filePath">File path</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogFileOperation(string operation, string filePath, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a plan generation event
    /// </summary>
    /// <param name="issueNumber">Issue number</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the generation succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogPlanGeneration(int issueNumber, long? durationMs = null, bool success = true, string? errorMessage = null);
    
    /// <summary>
    /// Log a plan execution event
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    /// <param name="success">Whether the execution succeeded</param>
    /// <param name="errorMessage">Error message if failed</param>
    void LogPlanExecution(string taskId, long? durationMs = null, bool success = true, string? errorMessage = null);
}
