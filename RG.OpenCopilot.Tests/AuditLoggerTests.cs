using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AuditLoggerTests {
    [Fact]
    public void LogAuditEvent_WithCompleteEvent_LogsAllFields() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.WebhookReceived,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            CorrelationId = "test-correlation-id",
            Description = "Test webhook received",
            Data = new Dictionary<string, object> {
                ["key1"] = "value1",
                ["key2"] = 42
            },
            Initiator = "test-user",
            Target = "test-resource",
            Result = "Success",
            DurationMs = 100
        };

        // Act
        auditLogger.LogAuditEvent(auditEvent);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("WebhookReceived");
        logger.LoggedMessages[0].ShouldContain("Test webhook received");
    }

    [Fact]
    public void LogWebhookReceived_WithEventType_LogsWebhookEvent() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var correlationId = "webhook-123";

        // Act
        auditLogger.LogWebhookReceived(
            eventType: "issues",
            correlationId: correlationId,
            data: new Dictionary<string, object> {
                ["action"] = "labeled",
                ["issueNumber"] = 42
            });

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("WebhookReceived");
        logger.LoggedMessages[0].ShouldContain("Webhook received: issues");
    }

    [Fact]
    public void LogWebhookValidation_ValidSignature_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var correlationId = "validation-123";

        // Act
        auditLogger.LogWebhookValidation(isValid: true, correlationId: correlationId);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("WebhookValidation");
        logger.LoggedMessages[0].ShouldContain("Valid");
    }

    [Fact]
    public void LogWebhookValidation_InvalidSignature_LogsWarning() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var correlationId = "validation-456";

        // Act
        auditLogger.LogWebhookValidation(isValid: false, correlationId: correlationId);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("WebhookValidation");
        logger.LoggedMessages[0].ShouldContain("Invalid");
    }

    [Fact]
    public void LogTaskStateTransition_TransitionsStates_LogsTransition() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var taskId = "owner/repo/issues/42";
        var correlationId = "task-transition-123";

        // Act
        auditLogger.LogTaskStateTransition(
            taskId: taskId,
            fromState: "PendingPlanning",
            toState: "Planned",
            correlationId: correlationId);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("TaskStateTransition");
        logger.LoggedMessages[0].ShouldContain("PendingPlanning");
        logger.LoggedMessages[0].ShouldContain("Planned");
    }

    [Fact]
    public void LogGitHubApiCall_SuccessfulCall_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var correlationId = "api-call-123";

        // Act
        auditLogger.LogGitHubApiCall(
            operation: "CreatePullRequest",
            correlationId: correlationId,
            durationMs: 250,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("GitHubApiCall");
        logger.LoggedMessages[0].ShouldContain("CreatePullRequest");
    }

    [Fact]
    public void LogGitHubApiCall_FailedCall_LogsError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var correlationId = "api-call-456";

        // Act
        auditLogger.LogGitHubApiCall(
            operation: "CreateBranch",
            correlationId: correlationId,
            durationMs: 100,
            success: false,
            errorMessage: "Rate limit exceeded");

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("GitHubApiCall");
        logger.LoggedMessages[0].ShouldContain("CreateBranch");
        logger.LoggedMessages[0].ShouldContain("Rate limit exceeded");
    }

    [Fact]
    public void LogJobStateTransition_TransitionsStates_LogsTransition() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var jobId = "job-12345";
        var correlationId = "job-transition-123";

        // Act
        auditLogger.LogJobStateTransition(
            jobId: jobId,
            fromState: "Queued",
            toState: "Processing",
            correlationId: correlationId);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("JobStateTransition");
        logger.LoggedMessages[0].ShouldContain("Queued");
        logger.LoggedMessages[0].ShouldContain("Processing");
    }

    [Fact]
    public void LogAuditEvent_WithNullOptionalFields_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            Description = "Test event",
            CorrelationId = null,
            Initiator = null,
            Target = null,
            Result = null,
            DurationMs = null,
            ErrorMessage = null
        };

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogAuditEvent(auditEvent));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogWebhookReceived_WithNullData_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogWebhookReceived(
            eventType: "ping",
            correlationId: "test-123",
            data: null));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogAuditEvent_WithAllEventTypes_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var eventTypes = new[] {
            AuditEventType.WebhookReceived,
            AuditEventType.WebhookValidation,
            AuditEventType.TaskStateTransition,
            AuditEventType.GitHubApiCall,
            AuditEventType.JobStateTransition,
            AuditEventType.ContainerOperation,
            AuditEventType.FileOperation,
            AuditEventType.PlanGeneration,
            AuditEventType.PlanExecution
        };

        // Act
        foreach (var eventType in eventTypes) {
            var auditEvent = new AuditEvent {
                EventType = eventType,
                Timestamp = timeProvider.GetUtcNow().DateTime,
                Description = $"Test {eventType}"
            };
            auditLogger.LogAuditEvent(auditEvent);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(eventTypes.Length);
        foreach (var eventType in eventTypes) {
            logger.LoggedMessages.ShouldContain(msg => msg.Contains(eventType.ToString()));
        }
    }

    [Fact]
    public void LogAuditEvent_WithSpecialCharactersInDescription_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.WebhookReceived,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            Description = "Test with special chars: <>&\"'\\n\\r\\t",
            CorrelationId = "test-123"
        };

        // Act
        auditLogger.LogAuditEvent(auditEvent);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("special chars");
    }

    [Fact]
    public void LogAuditEvent_WithLargeDataDictionary_LogsAllData() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var largeData = new Dictionary<string, object>();
        for (int i = 0; i < 50; i++) {
            largeData[$"key_{i}"] = $"value_{i}";
        }
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            Description = "Test with large data",
            Data = largeData
        };

        // Act
        auditLogger.LogAuditEvent(auditEvent);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("key_0");
        logger.LoggedMessages[0].ShouldContain("key_49");
    }

    [Fact]
    public void LogTaskStateTransition_WithMultipleTransitions_LogsAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var taskId = "owner/repo/issues/42";
        var correlationId = "task-123";

        var transitions = new[] {
            ("None", "PendingPlanning"),
            ("PendingPlanning", "Planned"),
            ("Planned", "Executing"),
            ("Executing", "Completed")
        };

        // Act
        foreach (var (from, to) in transitions) {
            auditLogger.LogTaskStateTransition(
                taskId: taskId,
                fromState: from,
                toState: to,
                correlationId: correlationId);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(transitions.Length);
        logger.LoggedMessages[0].ShouldContain("None");
        logger.LoggedMessages[0].ShouldContain("PendingPlanning");
        logger.LoggedMessages[3].ShouldContain("Executing");
        logger.LoggedMessages[3].ShouldContain("Completed");
    }

    [Fact]
    public void LogJobStateTransition_WithAllStatuses_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        var jobId = "job-123";
        var correlationId = "transition-456";

        var statuses = new[] {
            "Queued", "Processing", "Completed", "Failed", "Cancelled", "Retried", "DeadLetter"
        };

        // Act
        for (int i = 0; i < statuses.Length - 1; i++) {
            auditLogger.LogJobStateTransition(
                jobId: jobId,
                fromState: statuses[i],
                toState: statuses[i + 1],
                correlationId: correlationId);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(statuses.Length - 1);
        logger.LoggedMessages.ShouldAllBe(msg => msg.Contains("JobStateTransition"));
    }

    [Fact]
    public void LogGitHubApiCall_WithVeryLongDuration_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);

        // Act
        auditLogger.LogGitHubApiCall(
            operation: "CreatePullRequest",
            correlationId: "api-123",
            durationMs: 30000, // 30 seconds
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("30000");
    }

    [Fact]
    public void LogGitHubApiCall_WithNullCorrelationId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogGitHubApiCall(
            operation: "GetRepository",
            correlationId: null,
            durationMs: 100,
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogAuditEvent_WithComplexNestedData_SerializesCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.ContainerOperation,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            Description = "Container operation with nested data",
            Data = new Dictionary<string, object> {
                ["container"] = new { Id = "abc123", Image = "docker.io/test:latest" },
                ["metrics"] = new Dictionary<string, int> { ["cpu"] = 50, ["memory"] = 256 },
                ["tags"] = new[] { "production", "web-server" }
            }
        };

        // Act
        auditLogger.LogAuditEvent(auditEvent);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("Container operation");
    }

    [Fact]
    public void LogWebhookValidation_WithMultipleValidations_TracksAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);

        // Act
        auditLogger.LogWebhookValidation(isValid: true, correlationId: "valid-1");
        auditLogger.LogWebhookValidation(isValid: false, correlationId: "invalid-1");
        auditLogger.LogWebhookValidation(isValid: true, correlationId: "valid-2");
        auditLogger.LogWebhookValidation(isValid: false, correlationId: "invalid-2");

        // Assert
        logger.LoggedMessages.Count.ShouldBe(4);
        var validCount = logger.LoggedMessages.Count(msg => msg.Contains("Valid") && !msg.Contains("Invalid"));
        var invalidCount = logger.LoggedMessages.Count(msg => msg.Contains("Invalid"));
        validCount.ShouldBe(2);
        invalidCount.ShouldBe(2);
    }

    [Fact]
    public void LogAuditEvent_WithEmptyData_LogsSuccessfully() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(logger, timeProvider);
        
        var auditEvent = new AuditEvent {
            EventType = AuditEventType.PlanGeneration,
            Timestamp = timeProvider.GetUtcNow().DateTime,
            Description = "Plan generation started",
            Data = new Dictionary<string, object>() // Empty dictionary
        };

        // Act
        auditLogger.LogAuditEvent(auditEvent);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("Plan generation");
    }

    private class TestLogger<T> : ILogger<T> {
        public List<string> LoggedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            LoggedMessages.Add(formatter(state, exception));
        }
    }
}
