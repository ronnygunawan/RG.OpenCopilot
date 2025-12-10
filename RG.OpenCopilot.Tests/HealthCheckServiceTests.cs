using Microsoft.Extensions.DependencyInjection;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class HealthCheckServiceTests {
    [Fact]
    public async Task CheckHealthAsync_WithInMemoryStorage_ReturnsHealthy() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Components.ShouldContainKey("database");
        result.Components["database"].Status.ShouldBe(HealthStatus.Healthy);
        result.Components["database"].Description.ShouldContain("In-memory");
    }

    [Fact]
    public async Task CheckHealthAsync_WithEmptyQueue_ReturnsHealthyJobQueue() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components.ShouldContainKey("job_queue");
        result.Components["job_queue"].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithLargeQueue_ReturnsDegraded() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue(queueDepth: 1500); // > 1000 threshold
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components["job_queue"].Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WithHighFailureRate_ReturnsUnhealthy() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add jobs with high failure rate (> 50%)
        for (int i = 0; i < 10; i++) {
            await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = i < 6 ? BackgroundJobStatus.Failed : BackgroundJobStatus.Completed,
                CreatedAt = timeProvider.GetUtcNow().DateTime
            });
        }
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components.ShouldContainKey("job_processing");
        result.Components["job_processing"].Status.ShouldBe(HealthStatus.Unhealthy);
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithModerateFailureRate_ReturnsDegraded() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add jobs with moderate failure rate (between 20% and 50%)
        for (int i = 0; i < 10; i++) {
            await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = i < 3 ? BackgroundJobStatus.Failed : BackgroundJobStatus.Completed,
                CreatedAt = timeProvider.GetUtcNow().DateTime
            });
        }
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components.ShouldContainKey("job_processing");
        result.Components["job_processing"].Status.ShouldBe(HealthStatus.Degraded);
        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesMetrics() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add some jobs
        await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = timeProvider.GetUtcNow().DateTime
        });
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Metrics.ShouldContainKey("total_jobs");
        result.Metrics.ShouldContainKey("queue_depth");
        result.Metrics.ShouldContainKey("processing_count");
        result.Metrics.ShouldContainKey("failure_rate");
    }

    [Fact]
    public async Task CheckHealthAsync_WithAllHealthyComponents_ReturnsHealthy() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add successful jobs
        for (int i = 0; i < 5; i++) {
            await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = BackgroundJobStatus.Completed,
                CreatedAt = timeProvider.GetUtcNow().DateTime
            });
        }
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Components.Values.ShouldAllBe(c => c.Status == HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_SetsTimestamp() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var expectedTime = timeProvider.GetUtcNow().DateTime;
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Timestamp.ShouldBe(expectedTime);
    }

    [Fact]
    public async Task CheckHealthAsync_WithValidCancellationToken_CompletesSuccessfully() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        using var cts = new CancellationTokenSource();

        // Act
        var result = await healthCheckService.CheckHealthAsync(cts.Token);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithMixedComponentHealth_ReturnsCorrectOverallStatus() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue(queueDepth: 1500); // Degraded
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert - Overall should be degraded if any component is degraded
        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesAllRequiredComponents() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components.ShouldContainKey("database");
        result.Components.ShouldContainKey("job_queue");
        result.Components.ShouldContainKey("job_processing");
        result.Components.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task CheckHealthAsync_WithJobMetricsError_ReportsUnhealthyJobProcessing() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new FailingJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components.ShouldContainKey("job_processing");
        result.Components["job_processing"].Status.ShouldBe(HealthStatus.Unhealthy);
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithZeroJobs_ReturnsHealthyWithZeroMetrics() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Metrics["total_jobs"].ShouldBe(0);
    }

    [Fact]
    public async Task CheckHealthAsync_WithBoundaryFailureRate_ReturnsCorrectStatus() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add jobs with exactly 20% failure rate (boundary)
        for (int i = 0; i < 10; i++) {
            await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = i < 2 ? BackgroundJobStatus.Failed : BackgroundJobStatus.Completed,
                CreatedAt = timeProvider.GetUtcNow().DateTime
            });
        }
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert - At exactly 20%, should still be healthy (threshold is >20%)
        result.Components["job_processing"].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WithBoundaryQueueDepth_ReturnsCorrectStatus() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue(queueDepth: 1000); // Exactly at boundary
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert - At exactly 1000, should still be healthy (threshold is >1000)
        result.Components["job_queue"].Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_ComponentDetails_IncludeRelevantInformation() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue(queueDepth: 25);
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add some jobs
        for (int i = 0; i < 5; i++) {
            await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = $"job-{i}",
                JobType = "TestJob",
                Status = BackgroundJobStatus.Completed,
                CreatedAt = timeProvider.GetUtcNow().DateTime
            });
        }
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Components["database"].Details.ShouldContainKey("storage_type");
        result.Components["job_queue"].Details.ShouldContainKey("queue_depth");
        result.Components["job_queue"].Details["queue_depth"].ShouldBe(25);
        result.Components["job_processing"].Details.ShouldContainKey("queue_depth");
        result.Components["job_processing"].Details.ShouldContainKey("failure_rate");
    }

    [Fact]
    public async Task CheckHealthAsync_MetricsContainAllExpectedFields() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var jobStatusStore = new InMemoryJobStatusStore();
        var jobQueue = new TestJobQueue();
        var serviceProvider = CreateServiceProvider(hasDatabase: false);
        
        // Add a job to populate metrics
        await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            StartedAt = timeProvider.GetUtcNow().DateTime,
            CompletedAt = timeProvider.GetUtcNow().DateTime.AddSeconds(5),
            ProcessingDurationMs = 5000
        });
        
        var healthCheckService = new HealthCheckService(
            jobStatusStore,
            jobQueue,
            timeProvider,
            serviceProvider);

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        result.Metrics.ShouldContainKey("total_jobs");
        result.Metrics.ShouldContainKey("queue_depth");
        result.Metrics.ShouldContainKey("processing_count");
        result.Metrics.ShouldContainKey("failure_rate");
        result.Metrics.ShouldContainKey("average_processing_duration_ms");
    }

    private static IServiceProvider CreateServiceProvider(bool hasDatabase) {
        var services = new ServiceCollection();
        
        if (!hasDatabase) {
            // No database context registered - simulates in-memory storage
        }
        
        return services.BuildServiceProvider();
    }

    private class TestJobQueue : IJobQueue {
        private readonly int _queueDepth;

        public TestJobQueue(int queueDepth = 0) {
            _queueDepth = queueDepth;
        }

        public int Count => _queueDepth;

        public Task<bool> EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
            return Task.FromResult(true);
        }

        public Task<BackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult<BackgroundJob?>(null);
        }
    }

    private class FailingJobStatusStore : IJobStatusStore {
        public Task SetStatusAsync(BackgroundJobStatusInfo statusInfo, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<BackgroundJobStatusInfo?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default) {
            return Task.FromResult<BackgroundJobStatusInfo?>(null);
        }

        public Task DeleteStatusAsync(string jobId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsByStatusAsync(
            BackgroundJobStatus status,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsByTypeAsync(
            string jobType,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsBySourceAsync(
            string source,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsAsync(
            BackgroundJobStatus? status = null,
            string? jobType = null,
            string? source = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<JobMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("Failed to retrieve metrics");
        }
    }
}
