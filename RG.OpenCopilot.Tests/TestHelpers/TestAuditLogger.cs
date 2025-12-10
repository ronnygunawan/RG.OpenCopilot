namespace RG.OpenCopilot.Tests;

/// <summary>
/// Test implementation of IAuditLogger that does nothing
/// </summary>
internal sealed class TestAuditLogger : IAuditLogger {
    public void LogAuditEvent(AuditEvent auditEvent) { }
    public void LogWebhookReceived(string eventType, Dictionary<string, object>? data = null) { }
    public void LogWebhookValidation(bool isValid) { }
    public void LogTaskStateTransition(string taskId, string fromState, string toState) { }
    public void LogGitHubApiCall(string operation, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogJobStateTransition(string jobId, string fromState, string toState) { }
    public void LogContainerOperation(string operation, string? containerId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogFileOperation(string operation, string filePath, bool success = true, string? errorMessage = null) { }
    public void LogPlanGeneration(int issueNumber, long? durationMs = null, bool success = true, string? errorMessage = null) { }
    public void LogPlanExecution(string taskId, long? durationMs = null, bool success = true, string? errorMessage = null) { }
}
