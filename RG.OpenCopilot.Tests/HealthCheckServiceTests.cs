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
}
