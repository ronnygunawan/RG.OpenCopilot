using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;

public interface IWebhookHandler {
    Task<string> HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default);
}

public sealed class WebhookHandler : IWebhookHandler {
    private readonly IAgentTaskStore _taskStore;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IJobStatusStore _jobStatusStore;
    private readonly ILogger<WebhookHandler> _logger;

    public WebhookHandler(
        IAgentTaskStore taskStore,
        IJobDispatcher jobDispatcher,
        IJobStatusStore jobStatusStore,
        ILogger<WebhookHandler> logger) {
        _taskStore = taskStore;
        _jobDispatcher = jobDispatcher;
        _jobStatusStore = jobStatusStore;
        _logger = logger;
    }

    public async Task<string> HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default) {
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
                Status = AgentTaskStatus.PendingPlanning
            };

            await _taskStore.CreateTaskAsync(task, cancellationToken);
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
                Metadata = new Dictionary<string, string> {
                    ["TaskId"] = taskId,
                    ["RepositoryOwner"] = task.RepositoryOwner,
                    ["RepositoryName"] = task.RepositoryName,
                    ["IssueNumber"] = task.IssueNumber.ToString(),
                    ["WebhookId"] = jobPayload.WebhookId
                }
            };

            // Set initial job status
            await _jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
                JobId = job.Id,
                JobType = GeneratePlanJobHandler.JobTypeName,
                Status = BackgroundJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                Metadata = job.Metadata
            }, cancellationToken);

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
