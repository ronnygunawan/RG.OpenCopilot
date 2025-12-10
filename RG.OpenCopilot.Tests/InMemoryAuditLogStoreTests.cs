using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class InMemoryAuditLogStoreTests {
    private readonly InMemoryAuditLogStore _store;
    private readonly FakeTimeProvider _timeProvider;

    public InMemoryAuditLogStoreTests() {
        _store = new InMemoryAuditLogStore();
        _timeProvider = new FakeTimeProvider();
    }

    [Fact]
    public async Task StoreAsync_WithValidAuditLog_StoresSuccessfully() {
        // Arrange
        var auditLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            Description = "Test webhook"
        };

        // Act
        await _store.StoreAsync(auditLog);

        // Assert
        var results = await _store.QueryAsync();
        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(auditLog.Id);
    }

    [Fact]
    public async Task QueryAsync_FilterByEventType_ReturnsMatchingLogs() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync(eventType: AuditEventType.WebhookReceived);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldAllBe(log => log.EventType == AuditEventType.WebhookReceived);
    }

    [Fact]
    public async Task QueryAsync_FilterByCorrelationId_ReturnsMatchingLogs() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync(correlationId: "correlation-1");

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldAllBe(log => log.CorrelationId == "correlation-1");
    }

    [Fact]
    public async Task QueryAsync_WithLimit_ReturnsLimitedResults() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync(limit: 2);

        // Assert
        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task QueryAsync_WithExcessiveLimit_EnforcesMaximumLimit() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync(limit: 2000);

        // Assert
        results.Count.ShouldBe(5);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithValidRetentionPeriod_DeletesOldLogs() {
        // Arrange
        // Create logs with actual old timestamps
        var veryOldLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddDays(-100),
            Description = "Very old log"
        };
        var oldLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = DateTime.UtcNow.AddDays(-50),
            Description = "Old log"
        };
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.GitHubApiCall,
            Timestamp = DateTime.UtcNow.AddDays(-10),
            Description = "Recent log"
        };

        await _store.StoreAsync(veryOldLog);
        await _store.StoreAsync(oldLog);
        await _store.StoreAsync(recentLog);

        var retentionPeriod = TimeSpan.FromDays(30);

        // Act
        var deletedCount = await _store.DeleteOlderThanAsync(retentionPeriod);

        // Assert
        deletedCount.ShouldBe(2);
        var remainingLogs = await _store.QueryAsync();
        remainingLogs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithZeroRetentionPeriod_DeletesAllLogs() {
        // Arrange
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Description = "Log 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = DateTime.UtcNow.AddDays(-2),
                Description = "Log 2"
            }
        };

        foreach (var log in logs) {
            await _store.StoreAsync(log);
        }

        var retentionPeriod = TimeSpan.Zero;

        // Act
        var deletedCount = await _store.DeleteOlderThanAsync(retentionPeriod);

        // Assert
        deletedCount.ShouldBe(2);
        var remainingLogs = await _store.QueryAsync();
        remainingLogs.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithLargeRetentionPeriod_DeletesNoLogs() {
        // Arrange
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Description = "Recent log"
        };
        
        await _store.StoreAsync(recentLog);

        var retentionPeriod = TimeSpan.FromDays(365);

        // Act
        var deletedCount = await _store.DeleteOlderThanAsync(retentionPeriod);

        // Assert
        deletedCount.ShouldBe(0);
        var remainingLogs = await _store.QueryAsync();
        remainingLogs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_OrdersByTimestampDescending() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync();

        // Assert
        for (var i = 0; i < results.Count - 1; i++) {
            results[i].Timestamp.ShouldBeGreaterThanOrEqualTo(results[i + 1].Timestamp);
        }
    }

    private async Task SeedTestLogs() {
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = _timeProvider.GetUtcNow().DateTime,
                CorrelationId = "correlation-1",
                Description = "Webhook 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(1).DateTime,
                CorrelationId = "correlation-1",
                Description = "Task transition 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(2).DateTime,
                CorrelationId = "correlation-2",
                Description = "Webhook 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(3).DateTime,
                CorrelationId = "correlation-2",
                Description = "API call 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.PlanGeneration,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(4).DateTime,
                CorrelationId = "correlation-3",
                Description = "Plan generation 1"
            }
        };

        foreach (var log in logs) {
            await _store.StoreAsync(log);
        }
    }
}
