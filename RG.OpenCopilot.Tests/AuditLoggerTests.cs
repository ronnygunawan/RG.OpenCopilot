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

    private class TestLogger<T> : ILogger<T> {
        public List<string> LoggedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            LoggedMessages.Add(formatter(state, exception));
        }
    }
}
