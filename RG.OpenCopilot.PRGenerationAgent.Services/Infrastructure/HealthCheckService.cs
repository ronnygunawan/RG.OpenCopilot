using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Implementation of health check service
/// </summary>
internal sealed class HealthCheckService : IHealthCheckService {
    private readonly IJobStatusStore _jobStatusStore;
    private readonly IJobQueue _jobQueue;
    private readonly TimeProvider _timeProvider;
    private readonly IServiceProvider _serviceProvider;

    public HealthCheckService(
        IJobStatusStore jobStatusStore,
        IJobQueue jobQueue,
        TimeProvider timeProvider,
        IServiceProvider serviceProvider) {
        _jobStatusStore = jobStatusStore;
        _jobQueue = jobQueue;
        _timeProvider = timeProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default) {
        var components = new Dictionary<string, ComponentHealth>();
        var metrics = new Dictionary<string, object>();

        // Check database connectivity
        var databaseHealth = await CheckDatabaseHealthAsync(cancellationToken);
        components["database"] = databaseHealth;

        // Check job queue
        var queueHealth = CheckJobQueueHealth();
        components["job_queue"] = queueHealth;

        // Get job metrics
        try {
            var jobMetrics = await _jobStatusStore.GetMetricsAsync(cancellationToken);
            components["job_processing"] = new ComponentHealth {
                Status = DetermineJobProcessingHealth(jobMetrics),
                Description = "Background job processing system",
                Details = new Dictionary<string, object> {
                    ["queue_depth"] = jobMetrics.QueueDepth,
                    ["processing_count"] = jobMetrics.ProcessingCount,
                    ["completed_count"] = jobMetrics.CompletedCount,
                    ["failed_count"] = jobMetrics.FailedCount,
                    ["failure_rate"] = jobMetrics.FailureRate
                }
            };

            metrics["total_jobs"] = jobMetrics.TotalJobs;
            metrics["queue_depth"] = jobMetrics.QueueDepth;
            metrics["processing_count"] = jobMetrics.ProcessingCount;
            metrics["failure_rate"] = jobMetrics.FailureRate;
            metrics["average_processing_duration_ms"] = jobMetrics.AverageProcessingDurationMs;
        }
        catch (Exception ex) {
            components["job_processing"] = new ComponentHealth {
                Status = HealthStatus.Unhealthy,
                Description = "Failed to retrieve job metrics",
                Details = new Dictionary<string, object> {
                    ["error"] = ex.Message
                }
            };
        }

        // Determine overall health
        var overallStatus = DetermineOverallHealth(components.Values);

        return new HealthCheckResponse {
            Status = overallStatus,
            Timestamp = _timeProvider.GetUtcNow().DateTime,
            Components = components,
            Metrics = metrics
        };
    }

    private async Task<ComponentHealth> CheckDatabaseHealthAsync(CancellationToken cancellationToken) {
        try {
            // Try to get the DbContext from service provider
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetService<AgentTaskDbContext>();

            if (dbContext == null) {
                // Using in-memory storage
                return new ComponentHealth {
                    Status = HealthStatus.Healthy,
                    Description = "In-memory storage (no database configured)",
                    Details = new Dictionary<string, object> {
                        ["storage_type"] = "in-memory"
                    }
                };
            }

            // Check database connectivity
            await dbContext.Database.CanConnectAsync(cancellationToken);

            return new ComponentHealth {
                Status = HealthStatus.Healthy,
                Description = "Database connection successful",
                Details = new Dictionary<string, object> {
                    ["storage_type"] = "postgresql",
                    ["database"] = dbContext.Database.GetDbConnection().Database
                }
            };
        }
        catch (Exception ex) {
            return new ComponentHealth {
                Status = HealthStatus.Unhealthy,
                Description = "Database connection failed",
                Details = new Dictionary<string, object> {
                    ["error"] = ex.Message
                }
            };
        }
    }

    private ComponentHealth CheckJobQueueHealth() {
        try {
            var queueDepth = _jobQueue.Count;
            var status = queueDepth > 1000 ? HealthStatus.Degraded : HealthStatus.Healthy;

            return new ComponentHealth {
                Status = status,
                Description = "Job queue operational",
                Details = new Dictionary<string, object> {
                    ["queue_depth"] = queueDepth
                }
            };
        }
        catch (Exception ex) {
            return new ComponentHealth {
                Status = HealthStatus.Unhealthy,
                Description = "Job queue check failed",
                Details = new Dictionary<string, object> {
                    ["error"] = ex.Message
                }
            };
        }
    }

    private static HealthStatus DetermineJobProcessingHealth(JobMetrics metrics) {
        // Consider unhealthy if failure rate is > 50%
        if (metrics.FailureRate > 0.5) {
            return HealthStatus.Unhealthy;
        }

        // Consider degraded if failure rate is > 20% or queue is very deep
        if (metrics.FailureRate > 0.2 || metrics.QueueDepth > 500) {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    private static HealthStatus DetermineOverallHealth(IEnumerable<ComponentHealth> components) {
        var statuses = components.Select(c => c.Status).ToList();

        if (statuses.Any(s => s == HealthStatus.Unhealthy)) {
            return HealthStatus.Unhealthy;
        }

        if (statuses.Any(s => s == HealthStatus.Degraded)) {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }
}
