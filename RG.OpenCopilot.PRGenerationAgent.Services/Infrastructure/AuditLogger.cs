using System.Text.Json;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Implementation of audit logging service using structured logging
/// </summary>
internal sealed class AuditLogger : IAuditLogger {
    private readonly ILogger<AuditLogger> _logger;
    private readonly TimeProvider _timeProvider;

    public AuditLogger(ILogger<AuditLogger> logger, TimeProvider timeProvider) {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public void LogAuditEvent(AuditEvent auditEvent) {
        var eventData = new Dictionary<string, object> {
            ["EventType"] = auditEvent.EventType.ToString(),
            ["Timestamp"] = auditEvent.Timestamp,
            ["Description"] = auditEvent.Description
        };

        if (auditEvent.CorrelationId != null) {
            eventData["CorrelationId"] = auditEvent.CorrelationId;
        }

        if (auditEvent.Initiator != null) {
            eventData["Initiator"] = auditEvent.Initiator;
        }

        if (auditEvent.Target != null) {
            eventData["Target"] = auditEvent.Target;
        }

        if (auditEvent.Result != null) {
            eventData["Result"] = auditEvent.Result;
        }

        if (auditEvent.DurationMs.HasValue) {
            eventData["DurationMs"] = auditEvent.DurationMs.Value;
        }

        if (auditEvent.ErrorMessage != null) {
            eventData["ErrorMessage"] = auditEvent.ErrorMessage;
        }

        foreach (var kvp in auditEvent.Data) {
            eventData[kvp.Key] = kvp.Value;
        }

        _logger.LogInformation(
            "[AUDIT] {EventType}: {Description} | Data: {EventData}",
            auditEvent.EventType,
            auditEvent.Description,
            JsonSerializer.Serialize(eventData));
    }

    public void LogWebhookReceived(string eventType, string? correlationId, Dictionary<string, object>? data = null) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.WebhookReceived,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Webhook received: {eventType}",
            Data = data ?? [],
            Result = "Received"
        };

        LogAuditEvent(auditEvent);
    }

    public void LogWebhookValidation(bool isValid, string? correlationId) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.WebhookValidation,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Webhook signature validation: {(isValid ? "Valid" : "Invalid")}",
            Result = isValid ? "Success" : "Failure"
        };

        if (isValid) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogWarning(
                "[AUDIT] {EventType}: {Description} | CorrelationId: {CorrelationId}",
                auditEvent.EventType,
                auditEvent.Description,
                correlationId ?? "N/A");
        }
    }

    public void LogTaskStateTransition(string taskId, string fromState, string toState, string? correlationId) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Task state transition: {fromState} -> {toState}",
            Target = taskId,
            Data = new Dictionary<string, object> {
                ["TaskId"] = taskId,
                ["FromState"] = fromState,
                ["ToState"] = toState
            },
            Result = "Success"
        };

        LogAuditEvent(auditEvent);
    }

    public void LogGitHubApiCall(string operation, string? correlationId, long? durationMs = null, bool success = true, string? errorMessage = null) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.GitHubApiCall,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"GitHub API call: {operation}",
            Data = new Dictionary<string, object> {
                ["Operation"] = operation
            },
            DurationMs = durationMs,
            Result = success ? "Success" : "Failure",
            ErrorMessage = errorMessage
        };

        if (success) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogError(
                "[AUDIT] {EventType}: {Description} | CorrelationId: {CorrelationId} | Error: {ErrorMessage}",
                auditEvent.EventType,
                auditEvent.Description,
                correlationId ?? "N/A",
                errorMessage ?? "Unknown error");
        }
    }

    public void LogJobStateTransition(string jobId, string fromState, string toState, string? correlationId) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.JobStateTransition,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Job state transition: {fromState} -> {toState}",
            Target = jobId,
            Data = new Dictionary<string, object> {
                ["JobId"] = jobId,
                ["FromState"] = fromState,
                ["ToState"] = toState
            },
            Result = "Success"
        };

        LogAuditEvent(auditEvent);
    }
}
