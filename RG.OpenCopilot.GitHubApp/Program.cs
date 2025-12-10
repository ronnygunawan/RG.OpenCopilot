using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

public partial class Program {
    [ExcludeFromCodeCoverage]
    private static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Register TimeProvider.System for time operations
        builder.Services.AddSingleton(TimeProvider.System);

        // Add PR Generation Agent services
        builder.Services.AddPRGenerationAgentServices(builder.Configuration);

        var app = builder.Build();

        // Apply database migrations if PostgreSQL is configured
        app.Services.ApplyDatabaseMigrations();

        app.MapGet("/health", () => Results.Ok("ok"));

        app.MapGet("/health/detailed", async (IHealthCheckService healthCheckService) => {
            try {
                var healthCheck = await healthCheckService.CheckHealthAsync();
                
                return healthCheck.Status switch {
                    HealthStatus.Healthy => Results.Ok(healthCheck),
                    HealthStatus.Degraded => Results.Ok(healthCheck),
                    HealthStatus.Unhealthy => Results.Json(healthCheck, statusCode: 503),
                    _ => Results.Ok(healthCheck)
                };
            }
            catch (Exception ex) {
                return Results.Json(new {
                    status = "unhealthy",
                    error = ex.Message
                }, statusCode: 500);
            }
        });

        app.MapPost("/github/webhook", async (HttpContext context, IWebhookHandler handler, IWebhookValidator validator, IConfiguration config, ILogger<WebhookEndpoint> logger, IAuditLogger auditLogger) => {
            var correlationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            try {
                // Read the request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                // Validate signature if webhook secret is configured
                var webhookSecret = config["GitHub:WebhookSecret"];
                if (!string.IsNullOrEmpty(webhookSecret)) {
                    var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
                    var isValid = validator.ValidateSignature(body, signature, webhookSecret);
                    
                    auditLogger.LogWebhookValidation(isValid, correlationId);
                    
                    if (!isValid) {
                        logger.LogWarning("Invalid webhook signature");
                        return Results.Unauthorized();
                    }
                }

                var eventTypeSanitized = context.Request.Headers["X-GitHub-Event"].ToString().Replace("\r", "").Replace("\n", "");
                logger.LogInformation("Received webhook: {EventType}", eventTypeSanitized);

                // Check event type
                var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
                
                // Handle installation events
                if (eventType == "installation") {
                    var installationPayload = JsonSerializer.Deserialize<GitHubInstallationEventPayload>(body, new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    });

                    if (installationPayload == null) {
                        logger.LogWarning("Failed to parse installation webhook payload");
                        return Results.BadRequest("Invalid payload");
                    }

                    await handler.HandleInstallationEventAsync(installationPayload);
                    return Results.Ok();
                }

                // Handle issues events
                if (eventType == "issues") {
                    // Parse the payload
                    var payload = JsonSerializer.Deserialize<GitHubIssueEventPayload>(body, new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    });

                    if (payload == null) {
                        logger.LogWarning("Failed to parse webhook payload");
                        return Results.BadRequest("Invalid payload");
                    }

                    // Handle the event and get job ID
                    var jobId = await handler.HandleIssuesEventAsync(payload);

                    // Return 202 Accepted if a job was enqueued
                    if (!string.IsNullOrEmpty(jobId)) {
                        var statusUrl = $"{context.Request.Scheme}://{context.Request.Host}/jobs/{jobId}/status";
                        return Results.Accepted(statusUrl, new { jobId, statusUrl });
                    }

                    return Results.Ok();
                }

                // Ignore other event types
                var eventTypeSanitizedForLog = eventType.Replace("\r", "").Replace("\n", "");
                logger.LogInformation("Ignoring event: {EventType}", eventTypeSanitizedForLog);
                return Results.Ok();
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error processing webhook");
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/jobs/{jobId}/status", async (string jobId, IJobStatusStore statusStore, ILogger<JobStatusEndpoint> logger) => {
            try {
                var status = await statusStore.GetStatusAsync(jobId);
                if (status == null) {
                    return Results.NotFound(new { error = "Job not found" });
                }

                return Results.Ok(status);
            }
            catch (Exception ex) {
                // Sanitize jobId before logging to prevent log forging
                var jobIdSanitized = jobId.Replace("\r", "").Replace("\n", "");
                logger.LogError(ex, "Error retrieving job status for {JobId}", jobIdSanitized);
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/jobs", async (
            IJobStatusStore statusStore,
            ILogger<JobsEndpoint> logger,
            string? status = null,
            string? type = null,
            string? source = null,
            int skip = 0,
            int take = 100) => {
            try {
                BackgroundJobStatus? statusFilter = null;
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<BackgroundJobStatus>(status, ignoreCase: true, out var parsedStatus)) {
                    statusFilter = parsedStatus;
                }

                var jobs = await statusStore.GetJobsAsync(
                    status: statusFilter,
                    jobType: type,
                    source: source,
                    skip: skip,
                    take: take);

                return Results.Ok(new {
                    jobs,
                    count = jobs.Count,
                    skip,
                    take
                });
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error retrieving jobs");
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/jobs/metrics", async (IJobStatusStore statusStore, ILogger<JobMetricsEndpoint> logger) => {
            try {
                var metrics = await statusStore.GetMetricsAsync();
                return Results.Ok(metrics);
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error retrieving job metrics");
                return Results.StatusCode(500);
            }
        });

        app.MapGet("/jobs/dead-letter", async (
            IJobStatusStore statusStore,
            ILogger<DeadLetterEndpoint> logger,
            int skip = 0,
            int take = 100) => {
            try {
                var deadLetterJobs = await statusStore.GetJobsByStatusAsync(
                    BackgroundJobStatus.DeadLetter,
                    skip: skip,
                    take: take);

                return Results.Ok(new {
                    jobs = deadLetterJobs,
                    count = deadLetterJobs.Count,
                    skip,
                    take
                });
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error retrieving dead-letter jobs");
                return Results.StatusCode(500);
            }
        });

        app.MapPost("/jobs/{jobId}/cancel", (string jobId, IJobDispatcher jobDispatcher, ILogger<JobCancelEndpoint> logger) => {
            try {
                var cancelled = jobDispatcher.CancelJob(jobId);
                if (cancelled) {
                    logger.LogInformation("Cancelled job {JobId}", jobId);
                    return Results.Ok(new { jobId, cancelled = true });
                }

                logger.LogWarning("Job {JobId} not found or already completed", jobId);
                return Results.NotFound(new { error = "Job not found or already completed" });
            }
            catch (Exception ex) {
                var jobIdSanitized = jobId.Replace("\r", "").Replace("\n", "");
                logger.LogError(ex, "Error cancelling job {JobId}", jobIdSanitized);
                return Results.StatusCode(500);
            }
        });

        app.Run();
    }
}

// Marker class for logging in webhook endpoint
internal sealed class WebhookEndpoint { }

// Marker class for logging in job status endpoint
internal sealed class JobStatusEndpoint { }

// Marker class for logging in jobs list endpoint
internal sealed class JobsEndpoint { }

// Marker class for logging in metrics endpoint
internal sealed class JobMetricsEndpoint { }

// Marker class for logging in dead-letter endpoint
internal sealed class DeadLetterEndpoint { }

// Marker class for logging in job cancel endpoint
internal sealed class JobCancelEndpoint { }

