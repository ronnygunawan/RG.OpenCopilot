using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AuditLoggerExtendedTests {
    [Fact]
    public void LogContainerOperation_SuccessfulOperation_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var containerId = "abc123";

        // Act
        auditLogger.LogContainerOperation(
            operation: "CreateContainer",
            containerId: containerId,
            durationMs: 250,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("ContainerOperation");
        logger.LoggedMessages[0].ShouldContain("CreateContainer");
    }

    [Fact]
    public void LogContainerOperation_FailedOperation_LogsError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var containerId = "def456";

        // Act
        auditLogger.LogContainerOperation(
            operation: "CreateContainer",
            containerId: containerId,
            durationMs: 100,
            success: false,
            errorMessage: "Docker daemon not running");

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("ContainerOperation");
        logger.LoggedMessages[0].ShouldContain("CreateContainer");
        logger.LoggedMessages[0].ShouldContain("Docker daemon not running");
    }

    [Fact]
    public void LogContainerOperation_WithNullContainerId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogContainerOperation(
            operation: "CreateContainer",
            containerId: null,
            durationMs: 100,
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogFileOperation_SuccessfulOperation_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var filePath = "/workspace/src/MyClass.cs";

        // Act
        auditLogger.LogFileOperation(
            operation: "CreateFile",
            filePath: filePath,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("FileOperation");
        logger.LoggedMessages[0].ShouldContain("CreateFile");
        logger.LoggedMessages[0].ShouldContain(filePath);
    }

    [Fact]
    public void LogFileOperation_FailedOperation_LogsError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var filePath = "/workspace/src/MyClass.cs";

        // Act
        auditLogger.LogFileOperation(
            operation: "ModifyFile",
            filePath: filePath,
            success: false,
            errorMessage: "File not found");

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("FileOperation");
        logger.LoggedMessages[0].ShouldContain("ModifyFile");
        logger.LoggedMessages[0].ShouldContain("File not found");
    }

    [Fact]
    public void LogFileOperation_DifferentOperations_LogsAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var operations = new[] { "CreateFile", "ModifyFile", "DeleteFile" };

        // Act
        foreach (var operation in operations) {
            auditLogger.LogFileOperation(
                operation: operation,
                filePath: "/workspace/test.cs",
                success: true);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(operations.Length);
        foreach (var operation in operations) {
            logger.LoggedMessages.ShouldContain(msg => msg.Contains(operation));
        }
    }

    [Fact]
    public void LogPlanGeneration_SuccessfulGeneration_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var issueNumber = 42;

        // Act
        auditLogger.LogPlanGeneration(
            issueNumber: issueNumber,
            durationMs: 1500,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("PlanGeneration");
        logger.LoggedMessages[0].ShouldContain("issue #42");
    }

    [Fact]
    public void LogPlanGeneration_FailedGeneration_LogsError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var issueNumber = 43;

        // Act
        auditLogger.LogPlanGeneration(
            issueNumber: issueNumber,
            durationMs: 500,
            success: false,
            errorMessage: "LLM API error");

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("PlanGeneration");
        logger.LoggedMessages[0].ShouldContain("LLM API error");
    }

    [Fact]
    public void LogPlanExecution_SuccessfulExecution_LogsSuccess() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var taskId = "owner/repo/issues/42";

        // Act
        auditLogger.LogPlanExecution(
            taskId: taskId,
            durationMs: 5000,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("PlanExecution");
        logger.LoggedMessages[0].ShouldContain(taskId);
    }

    [Fact]
    public void LogPlanExecution_FailedExecution_LogsError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var taskId = "owner/repo/issues/43";

        // Act
        auditLogger.LogPlanExecution(
            taskId: taskId,
            durationMs: 2000,
            success: false,
            errorMessage: "Step execution failed");

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("AUDIT");
        logger.LoggedMessages[0].ShouldContain("PlanExecution");
        logger.LoggedMessages[0].ShouldContain("Step execution failed");
    }

    [Fact]
    public void LogContainerOperation_WithNullCorrelationId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogContainerOperation(
            operation: "CreateContainer",
            containerId: "abc123",
            durationMs: 100,
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogFileOperation_WithNullCorrelationId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogFileOperation(
            operation: "CreateFile",
            filePath: "/workspace/test.cs",
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogPlanGeneration_WithNullCorrelationId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogPlanGeneration(
            issueNumber: 42,
            durationMs: 1000,
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogPlanExecution_WithNullCorrelationId_DoesNotThrow() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act & Assert
        Should.NotThrow(() => auditLogger.LogPlanExecution(
            taskId: "test/repo/issues/42",
            durationMs: 5000,
            success: true));
        logger.LoggedMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public void LogContainerOperation_AllOperationTypes_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var operations = new[] { "CreateContainer", "CleanupContainer", "CommitAndPush", "ExecuteCommand" };

        // Act
        foreach (var operation in operations) {
            auditLogger.LogContainerOperation(
                operation: operation,
                containerId: "test-container",
                durationMs: 100,
                success: true);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(operations.Length);
        foreach (var operation in operations) {
            logger.LoggedMessages.ShouldContain(msg => msg.Contains(operation));
        }
    }

    [Fact]
    public void LogContainerOperation_WithVeryLongDuration_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());

        // Act
        auditLogger.LogContainerOperation(
            operation: "CreateContainer",
            containerId: "test-container",
            durationMs: 120000, // 2 minutes
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("120000");
    }

    [Fact]
    public void LogFileOperation_WithSpecialCharactersInPath_LogsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var filePath = "/workspace/src/My Class (v2).cs";

        // Act
        auditLogger.LogFileOperation(
            operation: "CreateFile",
            filePath: filePath,
            success: true);

        // Assert
        logger.LoggedMessages.ShouldNotBeEmpty();
        logger.LoggedMessages[0].ShouldContain("My Class");
    }

    [Fact]
    public void LogPlanGeneration_WithMultipleIssues_TracksAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var issueNumbers = new[] { 42, 43, 44, 45 };

        // Act
        foreach (var issueNumber in issueNumbers) {
            auditLogger.LogPlanGeneration(
                issueNumber: issueNumber,
                durationMs: 1000,
                success: true);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(issueNumbers.Length);
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("issue #42"));
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("issue #43"));
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("issue #44"));
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("issue #45"));
    }

    [Fact]
    public void LogPlanExecution_WithDifferentTasks_TracksAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var logger = new TestLogger<AuditLogger>();
        var correlationIdProvider = new TestCorrelationIdProvider();
        var auditLogger = new AuditLogger(logger, timeProvider, correlationIdProvider, new TestAuditLogStore());
        var taskIds = new[] {
            "owner/repo/issues/1",
            "owner/repo/issues/2",
            "owner/repo/issues/3"
        };

        // Act
        foreach (var taskId in taskIds) {
            auditLogger.LogPlanExecution(
                taskId: taskId,
                durationMs: 5000,
                success: true);
        }

        // Assert
        logger.LoggedMessages.Count.ShouldBe(taskIds.Length);
        foreach (var taskId in taskIds) {
            logger.LoggedMessages.ShouldContain(msg => msg.Contains(taskId));
        }
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
