using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AuditLogCleanupServiceTests {
    private readonly FakeTimeProvider _timeProvider;

    public AuditLogCleanupServiceTests() {
        _timeProvider = new FakeTimeProvider();
    }

    [Fact]
    public async Task CleanupAsync_WithDefaultRetentionPeriod_DeletesOldLogs() {
        // Arrange
        var store = new TestAuditLogStore();
        
        // Seed logs with actual old timestamps (older than default 90 days)
        var oldLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddDays(-100),
            Description = "Very old log"
        };
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = DateTime.UtcNow.AddDays(-30),
            Description = "Recent log"
        };
        
        await store.StoreAsync(oldLog);
        await store.StoreAsync(recentLog);
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        var deletedCount = await cleanupService.CleanupAsync();

        // Assert
        deletedCount.ShouldBe(1); // One log older than default 90 days
    }

    [Fact]
    public async Task CleanupAsync_WithCustomRetentionPeriod_DeletesOldLogs() {
        // Arrange
        var store = new TestAuditLogStore();
        
        // Seed logs with old timestamps
        var veryOldLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddDays(-10),
            Description = "Old log"
        };
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.TaskStateTransition,
            Timestamp = DateTime.UtcNow.AddDays(-1),
            Description = "Recent log"
        };
        
        await store.StoreAsync(veryOldLog);
        await store.StoreAsync(recentLog);
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AuditLog:RetentionDays"] = "2"
            })
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        var deletedCount = await cleanupService.CleanupAsync();

        // Assert
        deletedCount.ShouldBe(1); // One log older than 2 days
    }

    [Fact]
    public async Task CleanupAsync_WithVeryShortRetentionPeriod_DeletesAllOldLogs() {
        // Arrange
        var store = new TestAuditLogStore();
        
        // Seed logs that are definitely old (100 days ago)
        var oldLogs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = DateTime.UtcNow.AddDays(-100),
                Description = "Very old log 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = DateTime.UtcNow.AddDays(-95),
                Description = "Very old log 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Description = "Recent log"
            }
        };
        
        foreach (var log in oldLogs) {
            await store.StoreAsync(log);
        }
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AuditLog:RetentionDays"] = "90"
            })
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        var deletedCount = await cleanupService.CleanupAsync();

        // Assert
        deletedCount.ShouldBe(2); // Two logs older than 90 days
        var remainingLogs = await store.QueryAsync();
        remainingLogs.Count.ShouldBe(1);
        remainingLogs[0].Description.ShouldBe("Recent log");
    }

    [Fact]
    public async Task CleanupAsync_WithNoOldLogs_DeletesNothing() {
        // Arrange
        var store = new TestAuditLogStore();
        
        // Seed only recent logs
        var recentLog = new AuditLog {
            Id = Guid.NewGuid().ToString(),
            EventType = AuditEventType.WebhookReceived,
            Timestamp = DateTime.UtcNow.AddHours(-12),
            Description = "Recent log"
        };
        
        await store.StoreAsync(recentLog);
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AuditLog:RetentionDays"] = "1"
            })
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        var deletedCount = await cleanupService.CleanupAsync();

        // Assert
        deletedCount.ShouldBe(0);
        var remainingLogs = await store.QueryAsync();
        remainingLogs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CleanupAsync_LogsCleanupStart() {
        // Arrange
        var store = new TestAuditLogStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AuditLog:RetentionDays"] = "30"
            })
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        await cleanupService.CleanupAsync();

        // Assert
        logger.LogEntries.ShouldContain(entry => 
            entry.LogLevel == LogLevel.Information && 
            entry.Message.Contains("Starting audit log cleanup"));
    }

    [Fact]
    public async Task CleanupAsync_LogsCleanupCompletion() {
        // Arrange
        var store = new TestAuditLogStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["AuditLog:RetentionDays"] = "30"
            })
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(store, configuration, logger);

        // Act
        await cleanupService.CleanupAsync();

        // Assert
        logger.LogEntries.ShouldContain(entry => 
            entry.LogLevel == LogLevel.Information && 
            entry.Message.Contains("Audit log cleanup completed"));
    }

    [Fact]
    public async Task CleanupAsync_WithStoreError_ThrowsException() {
        // Arrange
        var mockStore = new Mock<IAuditLogStore>();
        mockStore
            .Setup(s => s.DeleteOlderThanAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(mockStore.Object, configuration, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await cleanupService.CleanupAsync());
        
        exception.Message.ShouldBe("Database error");
    }

    [Fact]
    public async Task CleanupAsync_WithStoreError_LogsError() {
        // Arrange
        var mockStore = new Mock<IAuditLogStore>();
        mockStore
            .Setup(s => s.DeleteOlderThanAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        
        var logger = new TestLogger<AuditLogCleanupService>();
        var cleanupService = new AuditLogCleanupService(mockStore.Object, configuration, logger);

        // Act
        try {
            await cleanupService.CleanupAsync();
        }
        catch {
            // Expected
        }

        // Assert
        logger.LogEntries.ShouldContain(entry => 
            entry.LogLevel == LogLevel.Error && 
            entry.Message.Contains("Failed to cleanup audit logs"));
    }

    private static async Task SeedTestLogs(TestAuditLogStore store, FakeTimeProvider timeProvider) {
        var logs = new[] {
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = timeProvider.GetUtcNow().DateTime,
                Description = "Webhook 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.TaskStateTransition,
                Timestamp = timeProvider.GetUtcNow().AddMinutes(1).DateTime,
                Description = "Task transition 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.WebhookReceived,
                Timestamp = timeProvider.GetUtcNow().AddMinutes(2).DateTime,
                Description = "Webhook 2"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.GitHubApiCall,
                Timestamp = timeProvider.GetUtcNow().AddMinutes(3).DateTime,
                Description = "API call 1"
            },
            new AuditLog {
                Id = Guid.NewGuid().ToString(),
                EventType = AuditEventType.PlanGeneration,
                Timestamp = timeProvider.GetUtcNow().AddMinutes(4).DateTime,
                Description = "Plan generation 1"
            }
        };

        foreach (var log in logs) {
            await store.StoreAsync(log);
        }
    }
}
