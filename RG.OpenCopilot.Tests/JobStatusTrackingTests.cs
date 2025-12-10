using Shouldly;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

namespace RG.OpenCopilot.Tests;

public class JobStatusTrackingTests {
    [Fact]
    public async Task InMemoryJobStatusStore_SetAndGetStatus_WorksCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        var statusInfo = new BackgroundJobStatusInfo {
            JobId = "test-job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "TestSource",
            Metadata = new Dictionary<string, string> { ["Key1"] = "Value1" }
        };

        // Act
        await store.SetStatusAsync(statusInfo);
        var retrieved = await store.GetStatusAsync("test-job-1");

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.JobId.ShouldBe("test-job-1");
        retrieved.JobType.ShouldBe("TestJob");
        retrieved.Status.ShouldBe(BackgroundJobStatus.Queued);
        retrieved.Source.ShouldBe("TestSource");
        retrieved.Metadata.ShouldContainKeyAndValue("Key1", "Value1");
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetStatus_ReturnsNullForNonexistentJob() {
        // Arrange
        var store = new InMemoryJobStatusStore();

        // Act
        var result = await store.GetStatusAsync("nonexistent-job");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task InMemoryJobStatusStore_DeleteStatus_RemovesJob() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        var statusInfo = new BackgroundJobStatusInfo {
            JobId = "test-job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        };

        await store.SetStatusAsync(statusInfo);

        // Act
        await store.DeleteStatusAsync("test-job-1");
        var retrieved = await store.GetStatusAsync("test-job-1");

        // Assert
        retrieved.ShouldBeNull();
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetJobsByStatus_FiltersCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });

        // Act
        var queuedJobs = await store.GetJobsByStatusAsync(BackgroundJobStatus.Queued);

        // Assert
        queuedJobs.Count.ShouldBe(2);
        queuedJobs.ShouldAllBe(j => j.Status == BackgroundJobStatus.Queued);
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetJobsByType_FiltersCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "ExecutePlan",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime
        });

        // Act
        var generatePlanJobs = await store.GetJobsByTypeAsync("GeneratePlan");

        // Assert
        generatePlanJobs.Count.ShouldBe(2);
        generatePlanJobs.ShouldAllBe(j => j.JobType == "GeneratePlan");
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetJobsBySource_FiltersCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Manual"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Webhook"
        });

        // Act
        var webhookJobs = await store.GetJobsBySourceAsync("Webhook");

        // Assert
        webhookJobs.Count.ShouldBe(2);
        webhookJobs.ShouldAllBe(j => j.Source == "Webhook");
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetJobs_WithMultipleFilters_FiltersCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Failed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "ExecutePlan",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            Source = "Webhook"
        });

        // Act
        var jobs = await store.GetJobsAsync(
            status: BackgroundJobStatus.Completed,
            jobType: "GeneratePlan",
            source: "Webhook");

        // Assert
        jobs.Count.ShouldBe(1);
        jobs[0].JobId.ShouldBe("job-1");
        jobs[0].JobType.ShouldBe("GeneratePlan");
        jobs[0].Status.ShouldBe(BackgroundJobStatus.Completed);
        jobs[0].Source.ShouldBe("Webhook");
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetJobs_WithPagination_ReturnsCorrectPage() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        for (int i = 0; i < 25; i++) {
            await store.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = BackgroundJobStatus.Completed,
                CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime.AddMinutes(-i)
            });
        }

        // Act
        var firstPage = await store.GetJobsAsync(skip: 0, take: 10);
        var secondPage = await store.GetJobsAsync(skip: 10, take: 10);
        var thirdPage = await store.GetJobsAsync(skip: 20, take: 10);

        // Assert
        firstPage.Count.ShouldBe(10);
        secondPage.Count.ShouldBe(10);
        thirdPage.Count.ShouldBe(5);
        
        // Jobs should be ordered by creation date descending
        firstPage[0].CreatedAt.ShouldBeGreaterThan(firstPage[9].CreatedAt);
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetMetrics_CalculatesCorrectly() {
        // Arrange
        var store = new InMemoryJobStatusStore();
        var now = new FakeTimeProvider().GetUtcNow().DateTime;
        
        // Add jobs with different statuses
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = now,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = now,
            StartedAt = now.AddSeconds(5),
            QueueWaitTimeMs = 5000,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "ExecutePlan",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = now,
            StartedAt = now.AddSeconds(2),
            CompletedAt = now.AddSeconds(12),
            ProcessingDurationMs = 10000,
            QueueWaitTimeMs = 2000,
            Source = "Webhook"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-4",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Failed,
            CreatedAt = now,
            StartedAt = now.AddSeconds(1),
            CompletedAt = now.AddSeconds(6),
            ProcessingDurationMs = 5000,
            QueueWaitTimeMs = 1000,
            ErrorMessage = "Test error",
            Source = "Manual"
        });
        await store.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-5",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.DeadLetter,
            CreatedAt = now,
            RetryCount = 3,
            MaxRetries = 3,
            Source = "Webhook"
        });

        // Act
        var metrics = await store.GetMetricsAsync();

        // Assert
        metrics.TotalJobs.ShouldBe(5);
        metrics.QueueDepth.ShouldBe(1);
        metrics.ProcessingCount.ShouldBe(1);
        metrics.CompletedCount.ShouldBe(1);
        metrics.FailedCount.ShouldBe(1);
        metrics.DeadLetterCount.ShouldBe(1);
        metrics.FailureRate.ShouldBe(0.2); // 1 failed out of 5 total
        metrics.AverageProcessingDurationMs.ShouldBe(7500.0); // (10000 + 5000) / 2
        metrics.AverageQueueWaitTimeMs.ShouldBe(2666.67, tolerance: 0.01); // (5000 + 2000 + 1000) / 3
        
        // Check metrics by type
        metrics.MetricsByType.ShouldContainKey("GeneratePlan");
        metrics.MetricsByType.ShouldContainKey("ExecutePlan");
        
        var generatePlanMetrics = metrics.MetricsByType["GeneratePlan"];
        generatePlanMetrics.TotalCount.ShouldBe(4);
        generatePlanMetrics.SuccessCount.ShouldBe(0);
        generatePlanMetrics.FailureCount.ShouldBe(1);
        generatePlanMetrics.FailureRate.ShouldBe(0.25); // 1 failed out of 4
    }

    [Fact]
    public async Task InMemoryJobStatusStore_GetMetrics_WithNoJobs_ReturnsZeroMetrics() {
        // Arrange
        var store = new InMemoryJobStatusStore();

        // Act
        var metrics = await store.GetMetricsAsync();

        // Assert
        metrics.TotalJobs.ShouldBe(0);
        metrics.QueueDepth.ShouldBe(0);
        metrics.ProcessingCount.ShouldBe(0);
        metrics.CompletedCount.ShouldBe(0);
        metrics.FailedCount.ShouldBe(0);
        metrics.DeadLetterCount.ShouldBe(0);
        metrics.FailureRate.ShouldBe(0.0);
        metrics.AverageProcessingDurationMs.ShouldBe(0.0);
        metrics.AverageQueueWaitTimeMs.ShouldBe(0.0);
        metrics.MetricsByType.ShouldBeEmpty();
    }

    [Fact]
    public void BackgroundJobStatus_HasAllRequiredStatuses() {
        // Arrange & Act
        var statuses = Enum.GetValues<BackgroundJobStatus>();

        // Assert
        statuses.ShouldContain(BackgroundJobStatus.Queued);
        statuses.ShouldContain(BackgroundJobStatus.Processing);
        statuses.ShouldContain(BackgroundJobStatus.Completed);
        statuses.ShouldContain(BackgroundJobStatus.Failed);
        statuses.ShouldContain(BackgroundJobStatus.Cancelled);
        statuses.ShouldContain(BackgroundJobStatus.Retried);
        statuses.ShouldContain(BackgroundJobStatus.DeadLetter);
    }

    [Fact]
    public void BackgroundJobStatusInfo_InitializesWithDefaults() {
        // Arrange & Act
        var statusInfo = new BackgroundJobStatusInfo();

        // Assert
        statusInfo.JobId.ShouldBe("");
        statusInfo.JobType.ShouldBe("");
        statusInfo.Status.ShouldBe(BackgroundJobStatus.Queued);
        statusInfo.Metadata.ShouldBeEmpty();
        statusInfo.RetryCount.ShouldBe(0);
        statusInfo.MaxRetries.ShouldBe(3);
        statusInfo.Source.ShouldBe("");
    }

    [Fact]
    public void BackgroundJobStatusInfo_StoresRetryInformation() {
        // Arrange & Act
        var now = new FakeTimeProvider().GetUtcNow().DateTime;
        var statusInfo = new BackgroundJobStatusInfo {
            JobId = "test-job",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Retried,
            CreatedAt = now,
            RetryCount = 2,
            MaxRetries = 3,
            LastRetryAt = now.AddMinutes(5)
        };

        // Assert
        statusInfo.RetryCount.ShouldBe(2);
        statusInfo.MaxRetries.ShouldBe(3);
        statusInfo.LastRetryAt.ShouldNotBeNull();
        statusInfo.LastRetryAt.Value.ShouldBe(now.AddMinutes(5));
    }

    [Fact]
    public void BackgroundJobStatusInfo_StoresCorrelationInformation() {
        // Arrange & Act
        var statusInfo = new BackgroundJobStatusInfo {
            JobId = "child-job",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            ParentJobId = "parent-job",
            CorrelationId = "correlation-123",
            Source = "Webhook"
        };

        // Assert
        statusInfo.ParentJobId.ShouldBe("parent-job");
        statusInfo.CorrelationId.ShouldBe("correlation-123");
        statusInfo.Source.ShouldBe("Webhook");
    }

    [Fact]
    public void BackgroundJobStatusInfo_StoresDurationMetrics() {
        // Arrange & Act
        var statusInfo = new BackgroundJobStatusInfo {
            JobId = "test-job",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = new FakeTimeProvider().GetUtcNow().DateTime,
            ProcessingDurationMs = 5000,
            QueueWaitTimeMs = 2000
        };

        // Assert
        statusInfo.ProcessingDurationMs.ShouldBe(5000);
        statusInfo.QueueWaitTimeMs.ShouldBe(2000);
    }

    [Fact]
    public void JobMetrics_InitializesWithDefaults() {
        // Arrange & Act
        var metrics = new JobMetrics();

        // Assert
        metrics.QueueDepth.ShouldBe(0);
        metrics.ProcessingCount.ShouldBe(0);
        metrics.CompletedCount.ShouldBe(0);
        metrics.FailedCount.ShouldBe(0);
        metrics.CancelledCount.ShouldBe(0);
        metrics.DeadLetterCount.ShouldBe(0);
        metrics.AverageProcessingDurationMs.ShouldBe(0.0);
        metrics.AverageQueueWaitTimeMs.ShouldBe(0.0);
        metrics.FailureRate.ShouldBe(0.0);
        metrics.TotalJobs.ShouldBe(0);
        metrics.MetricsByType.ShouldBeEmpty();
    }

    [Fact]
    public void JobTypeMetrics_InitializesCorrectly() {
        // Arrange & Act
        var metrics = new JobTypeMetrics {
            JobType = "TestJob",
            TotalCount = 10,
            SuccessCount = 8,
            FailureCount = 2,
            AverageProcessingDurationMs = 1500.0,
            FailureRate = 0.2
        };

        // Assert
        metrics.JobType.ShouldBe("TestJob");
        metrics.TotalCount.ShouldBe(10);
        metrics.SuccessCount.ShouldBe(8);
        metrics.FailureCount.ShouldBe(2);
        metrics.AverageProcessingDurationMs.ShouldBe(1500.0);
        metrics.FailureRate.ShouldBe(0.2);
    }
}
