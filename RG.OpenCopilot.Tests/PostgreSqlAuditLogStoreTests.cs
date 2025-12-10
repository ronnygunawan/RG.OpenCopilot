using Microsoft.EntityFrameworkCore;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class PostgreSqlAuditLogStoreTests : IDisposable {
    private readonly AgentTaskDbContext _context;
    private readonly PostgreSqlAuditLogStore _store;
    private readonly FakeTimeProvider _timeProvider;

    public PostgreSqlAuditLogStoreTests() {
        _timeProvider = new FakeTimeProvider();
        
        // Use in-memory SQLite database for testing
        var options = new DbContextOptionsBuilder<AgentTaskDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentTaskDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _store = new PostgreSqlAuditLogStore(_context);
    }

    public void Dispose() {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task StoreAsync_WithValidAuditLog_StoresSuccessfully() {
        // Arrange
        var auditLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = "test-correlation-id",
            Description = "Test webhook received",
            Data = new Dictionary<string, object> {
                ["eventType"] = "issues",
                ["action"] = "labeled"
            },
            Result = "Success"
        };

        // Act
        await _store.StoreAsync(auditLog);

        // Assert
        var storedLog = await _context.AuditLogs.FindAsync(auditLog.Id);
        storedLog.ShouldNotBeNull();
        storedLog.Id.ShouldBe(auditLog.Id);
        storedLog.EventType.ShouldBe(AuditEventType.WebhookReceived);
        storedLog.CorrelationId.ShouldBe("test-correlation-id");
        storedLog.Description.ShouldBe("Test webhook received");
        storedLog.Result.ShouldBe("Success");
        storedLog.Data.ShouldNotBeNull();
        storedLog.Data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StoreAsync_WithMultipleLogs_StoresAllLogs() {
        // Arrange
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = _timeProvider.GetUtcNow().DateTime,
                Description = "Log 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(1).DateTime,
                Description = "Log 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = _timeProvider.GetUtcNow().AddMinutes(2).DateTime,
                Description = "Log 3"
            }
        };

        // Act
        foreach (var log in logs) {
            await _store.StoreAsync(log);
        }

        // Assert
        var storedCount = await _context.AuditLogs.CountAsync();
        storedCount.ShouldBe(3);
    }

    [Fact]
    public async Task QueryAsync_WithNoFilters_ReturnsAllLogs() {
        // Arrange
        await SeedTestLogs();

        // Act
        var results = await _store.QueryAsync();

        // Assert
        results.Count.ShouldBe(5);
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
    public async Task QueryAsync_FilterByStartDate_ReturnsLogsAfterDate() {
        // Arrange
        await SeedTestLogs();
        var startDate = _timeProvider.GetUtcNow().AddMinutes(2).DateTime;

        // Act
        var results = await _store.QueryAsync(startDate: startDate);

        // Assert
        results.Count.ShouldBe(3);
        results.ShouldAllBe(log => log.Timestamp >= startDate);
    }

    [Fact]
    public async Task QueryAsync_FilterByEndDate_ReturnsLogsBeforeDate() {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = baseTime.AddMinutes(-5),
                Description = "Log 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = baseTime.AddMinutes(-3),
                Description = "Log 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = baseTime.AddMinutes(-1),
                Description = "Log 3"
            }
        };

        foreach (var log in logs) {
            await _store.StoreAsync(log);
        }

        var endDate = baseTime.AddMinutes(-2);

        // Act
        var results = await _store.QueryAsync(endDate: endDate);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldAllBe(log => log.Timestamp <= endDate);
    }

    [Fact]
    public async Task QueryAsync_FilterByDateRange_ReturnsLogsInRange() {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = baseTime.AddMinutes(-10),
                Description = "Log 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = baseTime.AddMinutes(-5),
                Description = "Log 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = baseTime.AddMinutes(-2),
                Description = "Log 3"
            }
        };

        foreach (var log in logs) {
            await _store.StoreAsync(log);
        }

        var startDate = baseTime.AddMinutes(-7);
        var endDate = baseTime.AddMinutes(-3);

        // Act
        var results = await _store.QueryAsync(startDate: startDate, endDate: endDate);

        // Assert
        results.Count.ShouldBe(1);
        results.ShouldAllBe(log => log.Timestamp >= startDate && log.Timestamp <= endDate);
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
        results.Count.ShouldBe(5); // All available logs
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

    [Fact]
    public async Task QueryAsync_WithMultipleFilters_ReturnsLogsMatchingAllFilters() {
        // Arrange
        await SeedTestLogs();
        var startDate = _timeProvider.GetUtcNow().AddMinutes(1).DateTime;

        // Act
        var results = await _store.QueryAsync(
            eventType: AuditEventType.TaskStateTransition,
            correlationId: "correlation-1",
            startDate: startDate);

        // Assert
        results.Count.ShouldBe(1);
        var log = results[0];
        log.EventType.ShouldBe(AuditEventType.TaskStateTransition);
        log.CorrelationId.ShouldBe("correlation-1");
        log.Timestamp.ShouldBeGreaterThanOrEqualTo(startDate);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithValidRetentionPeriod_DeletesOldLogs() {
        // Arrange
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Description = "Recent log"
        };
        
        await _store.StoreAsync(recentLog);

        var retentionPeriod = TimeSpan.FromDays(1); // Short period means all recent logs are retained

        // Act
        var deletedCount = await _store.DeleteOlderThanAsync(retentionPeriod);

        // Assert
        deletedCount.ShouldBe(0); // Log is only 1 hour old, within the 1-day retention period
        var remainingLogs = await _context.AuditLogs.CountAsync();
        remainingLogs.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithOldLogs_DeletesCorrectly() {
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
        deletedCount.ShouldBe(2); // Two logs older than 30 days
        var remainingLogs = await _context.AuditLogs.CountAsync();
        remainingLogs.ShouldBe(1);
        var remaining = await _context.AuditLogs.FirstAsync();
        remaining.Description.ShouldBe("Recent log");
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithZeroRetentionPeriod_DeletesAllLogs() {
        // Arrange  
        // Create logs with old timestamps
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
        var remainingLogs = await _context.AuditLogs.CountAsync();
        remainingLogs.ShouldBe(0);
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
        var remainingLogs = await _context.AuditLogs.CountAsync();
        remainingLogs.ShouldBe(1);
    }

    [Fact]
    public async Task StoreAsync_WithAllOptionalFields_StoresAllData() {
        // Arrange
        var auditLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.GitHubApiCall,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            CorrelationId = "correlation-123",
            Description = "API call to create PR",
            Data = new Dictionary<string, object> {
                ["repository"] = "owner/repo",
                ["prNumber"] = 42
            },
            Initiator = "webhook-handler",
            Target = "owner/repo/pull/42",
            Result = "Success",
            DurationMs = 1500,
            ErrorMessage = null
        };

        // Act
        await _store.StoreAsync(auditLog);

        // Assert
        var storedLog = await _context.AuditLogs.FindAsync(auditLog.Id);
        storedLog.ShouldNotBeNull();
        storedLog.Initiator.ShouldBe("webhook-handler");
        storedLog.Target.ShouldBe("owner/repo/pull/42");
        storedLog.DurationMs.ShouldBe(1500);
    }

    [Fact]
    public async Task StoreAsync_WithErrorMessage_StoresErrorDetails() {
        // Arrange
        var auditLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.GitHubApiCall,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            Description = "Failed API call",
            Result = "Failure",
            ErrorMessage = "Rate limit exceeded"
        };

        // Act
        await _store.StoreAsync(auditLog);

        // Assert
        var storedLog = await _context.AuditLogs.FindAsync(auditLog.Id);
        storedLog.ShouldNotBeNull();
        storedLog.Result.ShouldBe("Failure");
        storedLog.ErrorMessage.ShouldBe("Rate limit exceeded");
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
