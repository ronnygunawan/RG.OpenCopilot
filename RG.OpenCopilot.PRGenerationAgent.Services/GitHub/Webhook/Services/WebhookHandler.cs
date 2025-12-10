using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;

public interface IWebhookHandler {
    Task<string> HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default);
    Task HandleInstallationEventAsync(GitHubInstallationEventPayload payload, CancellationToken cancellationToken = default);
}

public sealed class WebhookHandler : IWebhookHandler {
    private readonly IAgentTaskStore _taskStore;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IJobStatusStore _jobStatusStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebhookHandler> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly ICorrelationIdProvider _correlationIdProvider;

    public WebhookHandler(
        IAgentTaskStore taskStore,
        IJobDispatcher jobDispatcher,
        IJobStatusStore jobStatusStore,
        TimeProvider timeProvider,
        ILogger<WebhookHandler> logger,
        IAuditLogger auditLogger,
        ICorrelationIdProvider correlationIdProvider) {
        _taskStore = taskStore;
        _jobDispatcher = jobDispatcher;
        _jobStatusStore = jobStatusStore;
        _timeProvider = timeProvider;
        _logger = logger;
        _auditLogger = auditLogger;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task HandleInstallationEventAsync(GitHubInstallationEventPayload payload, CancellationToken cancellationToken = default) {
        var correlationId = _correlationIdProvider.GenerateCorrelationId();
        
        _auditLogger.LogWebhookReceived(
            eventType: "installation",
            data: new Dictionary<string, object> {
                ["Action"] = payload.Action,
                ["InstallationId"] = payload.Installation?.Id ?? 0
            });

        if (payload.Action != "deleted" || payload.Installation == null) {
            _logger.LogInformation("Ignoring installation event: action={Action}", payload.Action);
            return;
        }

        _logger.LogInformation("Processing GitHub App uninstallation for installation {InstallationId}", payload.Installation.Id);

        try {
            // Get all tasks for this installation
            var tasks = await _taskStore.GetTasksByInstallationIdAsync(payload.Installation.Id, cancellationToken);

            foreach (var task in tasks) {
                // Cancel task if it's in progress
                if (task.Status == AgentTaskStatus.PendingPlanning || 
                    task.Status == AgentTaskStatus.Planned || 
                    task.Status == AgentTaskStatus.Executing) {
                    
                    var previousStatus = task.Status.ToString();
                    task.Status = AgentTaskStatus.Cancelled;
                    task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
                    await _taskStore.UpdateTaskAsync(task, cancellationToken);
                    
                    _auditLogger.LogTaskStateTransition(
                        taskId: task.Id,
                        fromState: previousStatus,
                        toState: AgentTaskStatus.Cancelled.ToString());
                    
                    _logger.LogInformation("Cancelled task {TaskId} due to app uninstallation", task.Id);
                }
            }

            // Find and cancel any active jobs for this installation
            // Use pagination to handle large numbers of jobs
            const int pageSize = 100;
            var skip = 0;
            var hasMore = true;

            while (hasMore) {
                var jobs = await _jobStatusStore.GetJobsAsync(
                    status: null,
                    jobType: null,
                    source: null,
                    skip: skip,
                    take: pageSize,
                    cancellationToken);

                if (jobs.Count == 0) {
                    hasMore = false;
                    break;
                }

                foreach (var job in jobs) {
                    if (job.Status == BackgroundJobStatus.Queued || job.Status == BackgroundJobStatus.Processing) {
                        // Check if job metadata contains InstallationId
                        if (job.Metadata.TryGetValue("InstallationId", out var installationIdStr) &&
                            long.TryParse(installationIdStr, out var installationId) &&
                            installationId == payload.Installation.Id) {
                            
                            _jobDispatcher.CancelJob(job.JobId);
                            _logger.LogInformation("Cancelled job {JobId} due to app uninstallation", job.JobId);
                        }
                    }
                }

                skip += pageSize;
                hasMore = jobs.Count == pageSize;
            }

            _logger.LogInformation("Completed handling uninstallation for installation {InstallationId}", payload.Installation.Id);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error handling installation event for installation {InstallationId}", payload.Installation.Id);
            throw;
        }
    }

    public async Task<string> HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default) {
        var correlationId = _correlationIdProvider.GenerateCorrelationId();
        
        _auditLogger.LogWebhookReceived(
            eventType: "issues",
            data: new Dictionary<string, object> {
                ["Action"] = payload.Action,
                ["Label"] = payload.Label?.Name ?? "N/A",
                ["IssueNumber"] = payload.Issue?.Number ?? 0,
                ["Repository"] = payload.Repository?.Full_Name ?? "N/A"
            });

        // Check if this is a label event with the copilot-assisted label
        if (payload.Action != "labeled" || payload.Label?.Name != "copilot-assisted") {
            _logger.LogInformation("Ignoring issues event: action={Action}, label={Label}",
                payload.Action, payload.Label?.Name);
            return "";
        }

        if (payload.Issue == null || payload.Repository == null || payload.Installation == null) {
            _logger.LogWarning("Received issues event with missing required fields");
            return "";
        }

        _logger.LogInformation("Processing copilot-assisted label for issue #{IssueNumber} in {Repo}",
            payload.Issue.Number, payload.Repository.Full_Name);

        try {
            // Create an agent task
            var taskId = $"{payload.Repository.Full_Name}/issues/{payload.Issue.Number}";
            var existingTask = await _taskStore.GetTaskAsync(taskId, cancellationToken);

            if (existingTask != null) {
                _logger.LogInformation("Task {TaskId} already exists, skipping", taskId);
                return "";
            }

            var task = new AgentTask {
                Id = taskId,
                InstallationId = payload.Installation.Id,
                RepositoryOwner = payload.Repository.Owner?.Login ?? "",
                RepositoryName = payload.Repository.Name,
                IssueNumber = payload.Issue.Number,
                Status = AgentTaskStatus.PendingPlanning,
                CreatedAt = _timeProvider.GetUtcNow().DateTime
            };

            await _taskStore.CreateTaskAsync(task, cancellationToken);
            
            _auditLogger.LogTaskStateTransition(
                taskId: taskId,
                fromState: "None",
                toState: AgentTaskStatus.PendingPlanning.ToString());
            
            _logger.LogInformation("Created task {TaskId}", taskId);

            // Enqueue plan generation job
            var jobPayload = new GeneratePlanJobPayload {
                TaskId = taskId,
                InstallationId = payload.Installation.Id,
                RepositoryOwner = payload.Repository.Owner?.Login ?? "",
                RepositoryName = payload.Repository.Name,
                IssueNumber = payload.Issue.Number,
                IssueTitle = payload.Issue.Title,
                IssueBody = payload.Issue.Body ?? "",
                WebhookId = Guid.NewGuid().ToString()
            };

            var job = new BackgroundJob {
                Type = GeneratePlanJobHandler.JobTypeName,
                Payload = JsonSerializer.Serialize(jobPayload),
                Priority = 5, // Higher priority for plan generation
                CreatedAt = _timeProvider.GetUtcNow().DateTime,
                Metadata = new Dictionary<string, string> {
                    ["TaskId"] = taskId,
                    ["InstallationId"] = payload.Installation.Id.ToString(),
                    ["RepositoryOwner"] = task.RepositoryOwner,
                    ["RepositoryName"] = task.RepositoryName,
                    ["IssueNumber"] = task.IssueNumber.ToString(),
                    ["WebhookId"] = jobPayload.WebhookId,
                    ["CorrelationId"] = correlationId
                }
            };

            // Set initial job status
            await _jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = job.Id,
                JobType = GeneratePlanJobHandler.JobTypeName,
                Status = BackgroundJobStatus.Queued,
                CreatedAt = _timeProvider.GetUtcNow().DateTime,
                Metadata = job.Metadata,
                CorrelationId = correlationId
            }, cancellationToken);

            _auditLogger.LogJobStateTransition(
                jobId: job.Id,
                fromState: "None",
                toState: BackgroundJobStatus.Queued.ToString());

            var dispatched = await _jobDispatcher.DispatchAsync(job, cancellationToken);
            if (dispatched) {
                _logger.LogInformation("Dispatched plan generation job {JobId} for task {TaskId}", job.Id, taskId);
                return job.Id;
            }
            else {
                _logger.LogWarning("Failed to dispatch plan generation job for task {TaskId}", taskId);
                return "";
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error handling issues event for issue #{IssueNumber}", payload.Issue.Number);
            throw;
        }
    }
}
