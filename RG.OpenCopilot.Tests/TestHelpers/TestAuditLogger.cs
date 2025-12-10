namespace RG.OpenCopilot.Tests;

/// <summary>
/// Test implementation of IAuditLogger that does nothing
/// </summary>
internal sealed class TestAuditLogger : IAuditLogger {
    public void LogAuditEvent(AuditEvent auditEvent) { }
    public void LogWebhookReceived(string eventType, string? correlationId, Dictionary<string, object>? data = null) { }
    public void LogWebhookValidation(bool isValid, string? correlationId) { }
    public void LogTaskStateTransition(string taskId, string fromState, string toState, string? correlationId) { }
    public void LogGitHubApiCall(string operation, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogJobStateTransition(string jobId, string fromState, string toState, string? correlationId) { }
    public void LogContainerOperation(string operation, string? containerId, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogFileOperation(string operation, string filePath, string? correlationId, bool success = true, string? errorMessage = null) { }
    public void LogPlanGeneration(int issueNumber, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogPlanExecution(string taskId, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
}
