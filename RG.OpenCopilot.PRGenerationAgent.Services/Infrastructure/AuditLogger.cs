using System.Text.Json;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Implementation of audit logging service using structured logging
/// </summary>
internal sealed class AuditLogger : IAuditLogger {
    private readonly ILogger<AuditLogger> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public AuditLogger(ILogger<AuditLogger> logger, TimeProvider timeProvider, ICorrelationIdProvider correlationIdProvider) {
        _logger = logger;
        _timeProvider = timeProvider;
        _correlationIdProvider = correlationIdProvider;
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

    public void LogWebhookReceived(string eventType, Dictionary<string, object>? data = null) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.WebhookReceived,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = _correlationIdProvider.GetCorrelationId(),
            Description = $"Webhook received: {eventType}",
            Data = data ?? [],
            Result = "Received"
        };

        LogAuditEvent(auditEvent);
    }

    public void LogWebhookValidation(bool isValid) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
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

    public void LogTaskStateTransition(string taskId, string fromState, string toState) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = _correlationIdProvider.GetCorrelationId(),
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

    public void LogGitHubApiCall(string operation, long? durationMs = null, bool success = true, string? errorMessage = null) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
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

    public void LogJobStateTransition(string jobId, string fromState, string toState) {
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.JobStateTransition,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = _correlationIdProvider.GetCorrelationId(),
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

    public void LogContainerOperation(string operation, string? containerId, long? durationMs = null, bool success = true, string? errorMessage = null) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.ContainerOperation,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Container operation: {operation}",
            Target = containerId,
            Data = new Dictionary<string, object> {
                ["Operation"] = operation
            },
            DurationMs = durationMs,
            Result = success ? "Success" : "Failure",
            ErrorMessage = errorMessage
        };

        if (containerId != null) {
            auditEvent.Data["ContainerId"] = containerId;
        }

        if (success) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogError(
                "[AUDIT] {EventType}: {Description} | ContainerId: {ContainerId} | Error: {ErrorMessage}",
                auditEvent.EventType,
                auditEvent.Description,
                containerId ?? "N/A",
                errorMessage ?? "Unknown error");
        }
    }

    public void LogFileOperation(string operation, string filePath, bool success = true, string? errorMessage = null) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.FileOperation,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"File operation: {operation}",
            Target = filePath,
            Data = new Dictionary<string, object> {
                ["Operation"] = operation,
                ["FilePath"] = filePath
            },
            Result = success ? "Success" : "Failure",
            ErrorMessage = errorMessage
        };

        if (success) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogError(
                "[AUDIT] {EventType}: {Description} | FilePath: {FilePath} | Error: {ErrorMessage}",
                auditEvent.EventType,
                auditEvent.Description,
                filePath,
                errorMessage ?? "Unknown error");
        }
    }

    public void LogPlanGeneration(int issueNumber, long? durationMs = null, bool success = true, string? errorMessage = null) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.PlanGeneration,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Plan generation for issue #{issueNumber}",
            Target = $"issue-{issueNumber}",
            Data = new Dictionary<string, object> {
                ["IssueNumber"] = issueNumber
            },
            DurationMs = durationMs,
            Result = success ? "Success" : "Failure",
            ErrorMessage = errorMessage
        };

        if (success) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogError(
                "[AUDIT] {EventType}: {Description} | IssueNumber: {IssueNumber} | Error: {ErrorMessage}",
                auditEvent.EventType,
                auditEvent.Description,
                issueNumber,
                errorMessage ?? "Unknown error");
        }
    }

    public void LogPlanExecution(string taskId, long? durationMs = null, bool success = true, string? errorMessage = null) {
        var correlationId = _correlationIdProvider.GetCorrelationId();
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.PlanExecution,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = correlationId,
            Description = $"Plan execution for task {taskId}",
            Target = taskId,
            Data = new Dictionary<string, object> {
                ["TaskId"] = taskId
            },
            DurationMs = durationMs,
            Result = success ? "Success" : "Failure",
            ErrorMessage = errorMessage
        };

        if (success) {
            LogAuditEvent(auditEvent);
        } else {
            _logger.LogError(
                "[AUDIT] {EventType}: {Description} | TaskId: {TaskId} | Error: {ErrorMessage}",
                auditEvent.EventType,
                auditEvent.Description,
                taskId,
                errorMessage ?? "Unknown error");
        }
    }
}
