using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Payload for plan generation jobs
/// </summary>
public sealed class GeneratePlanJobPayload {
    public string TaskId { get; init; } = "";
    public long InstallationId { get; init; }
    public string RepositoryOwner { get; init; } = "";
    public string RepositoryName { get; init; } = "";
    public int IssueNumber { get; init; }
    public string IssueTitle { get; init; } = "";
    public string IssueBody { get; init; } = "";
    public string WebhookId { get; init; } = "";
}

/// <summary>
/// Job handler for generating agent plans
/// </summary>
internal sealed class GeneratePlanJobHandler : IJobHandler {
    public const string JobTypeName = "GeneratePlan";
    
    private readonly IAgentTaskStore _taskStore;
    private readonly IPlannerService _plannerService;
    private readonly IGitHubService _gitHubService;
    private readonly IRepositoryAnalyzer _repositoryAnalyzer;
    private readonly IInstructionsLoader _instructionsLoader;
    private readonly IJobDispatcher _jobDispatcher;
    private readonly IJobStatusStore _jobStatusStore;
    private readonly BackgroundJobOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GeneratePlanJobHandler> _logger;

    public string JobType => JobTypeName;

    public GeneratePlanJobHandler(
        IAgentTaskStore taskStore,
        IPlannerService plannerService,
        IGitHubService gitHubService,
        IRepositoryAnalyzer repositoryAnalyzer,
        IInstructionsLoader instructionsLoader,
        IJobDispatcher jobDispatcher,
        IJobStatusStore jobStatusStore,
        BackgroundJobOptions options,
        TimeProvider timeProvider,
        ILogger<GeneratePlanJobHandler> logger) {
        _taskStore = taskStore;
        _plannerService = plannerService;
        _gitHubService = gitHubService;
        _repositoryAnalyzer = repositoryAnalyzer;
        _instructionsLoader = instructionsLoader;
        _jobDispatcher = jobDispatcher;
        _jobStatusStore = jobStatusStore;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
        try {
            // Update job status to Processing
            await UpdateJobStatusAsync(jobId: job.Id, status: BackgroundJobStatus.Processing, job.Metadata, cancellationToken);

            // Apply timeout if configured
            CancellationTokenSource? timeoutCts = null;
            CancellationToken effectiveCancellationToken = cancellationToken;

            if (_options.PlanTimeoutSeconds > 0) {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.PlanTimeoutSeconds));
                effectiveCancellationToken = timeoutCts.Token;
                _logger.LogInformation("Plan generation timeout set to {TimeoutSeconds} seconds", _options.PlanTimeoutSeconds);
            }

            try {
                return await ExecutePlanGenerationAsync(job, effectiveCancellationToken);
            }
            finally {
                timeoutCts?.Dispose();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            // Timeout occurred
            _logger.LogWarning("Plan generation timed out for job {JobId} after {TimeoutSeconds} seconds", 
                job.Id, _options.PlanTimeoutSeconds);
            await UpdateJobStatusAsync(
                jobId: job.Id,
                status: BackgroundJobStatus.Failed,
                job.Metadata,
                cancellationToken,
                errorMessage: $"Plan generation timed out after {_options.PlanTimeoutSeconds} seconds");
            return JobResult.CreateFailure(
                errorMessage: $"Plan generation timed out after {_options.PlanTimeoutSeconds} seconds",
                shouldRetry: false);
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Plan generation cancelled for job {JobId}", job.Id);
            await UpdateJobStatusAsync(
                jobId: job.Id,
                status: BackgroundJobStatus.Cancelled,
                job.Metadata,
                cancellationToken);
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to generate plan for job {JobId}", job.Id);
            await UpdateJobStatusAsync(
                jobId: job.Id,
                status: BackgroundJobStatus.Failed,
                job.Metadata,
                cancellationToken,
                errorMessage: ex.Message);
            return JobResult.CreateFailure(errorMessage: ex.Message, exception: ex, shouldRetry: true);
        }
    }

    private async Task<JobResult> ExecutePlanGenerationAsync(BackgroundJob job, CancellationToken cancellationToken) {
        // Deserialize payload
        var payload = JsonSerializer.Deserialize<GeneratePlanJobPayload>(job.Payload);
        if (payload == null) {
            await UpdateJobStatusAsync(
                jobId: job.Id,
                status: BackgroundJobStatus.Failed,
                job.Metadata,
                cancellationToken,
                errorMessage: "Failed to deserialize job payload");
            return JobResult.CreateFailure(errorMessage: "Failed to deserialize job payload", shouldRetry: false);
        }

        _logger.LogInformation(
            "Generating plan for issue #{IssueNumber} in {Repo}",
            payload.IssueNumber,
            $"{payload.RepositoryOwner}/{payload.RepositoryName}");

        // Create working branch
        var branchName = await _gitHubService.CreateWorkingBranchAsync(
            payload.RepositoryOwner,
            payload.RepositoryName,
            payload.IssueNumber,
            cancellationToken);

        // Create WIP PR with initial issue prompt
        var prNumber = await _gitHubService.CreateWipPullRequestAsync(
            payload.RepositoryOwner,
            payload.RepositoryName,
            branchName,
            payload.IssueNumber,
            payload.IssueTitle,
            payload.IssueBody,
            cancellationToken);

        _logger.LogInformation("Created WIP PR #{PrNumber} for task {TaskId}", prNumber, payload.TaskId);

        // Analyze repository to gather context
        RepositoryAnalysis? repoAnalysis = null;
        try {
            repoAnalysis = await _repositoryAnalyzer.AnalyzeAsync(
                payload.RepositoryOwner,
                payload.RepositoryName,
                cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to analyze repository, proceeding without analysis");
        }

        // Load custom instructions if available
        string? instructions = null;
        try {
            instructions = await _instructionsLoader.LoadInstructionsAsync(
                payload.RepositoryOwner,
                payload.RepositoryName,
                payload.IssueNumber,
                cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to load instructions, proceeding without them");
        }

        // Generate plan with all available context
        var context = new AgentTaskContext {
            IssueTitle = payload.IssueTitle,
            IssueBody = payload.IssueBody,
            RepositorySummary = repoAnalysis?.Summary,
            InstructionsMarkdown = instructions
        };

        var plan = await _plannerService.CreatePlanAsync(context, cancellationToken);
        
        // Update task with plan
        var task = await _taskStore.GetTaskAsync(payload.TaskId, cancellationToken);
        if (task == null) {
            await UpdateJobStatusAsync(
                jobId: job.Id,
                status: BackgroundJobStatus.Failed,
                job.Metadata,
                cancellationToken,
                errorMessage: $"Task {payload.TaskId} not found");
            return JobResult.CreateFailure(errorMessage: $"Task {payload.TaskId} not found", shouldRetry: false);
        }

        task.Plan = plan;
        task.Status = AgentTaskStatus.Planned;
        await _taskStore.UpdateTaskAsync(task, cancellationToken);

        _logger.LogInformation("Generated plan for task {TaskId}", payload.TaskId);

        // Update PR description with the plan
        var updatedPrBody = FormatPrBodyWithPlan(
            payload.IssueNumber,
            payload.IssueTitle,
            payload.IssueBody,
            plan);
        await _gitHubService.UpdatePullRequestDescriptionAsync(
            payload.RepositoryOwner,
            payload.RepositoryName,
            prNumber,
            $"[WIP] {payload.IssueTitle}",
            updatedPrBody,
            cancellationToken);

        _logger.LogInformation("Updated PR #{PrNumber} with plan for task {TaskId}", prNumber, payload.TaskId);

        // Dispatch job to execute the plan in the background
        var executionPayload = new ExecutePlanJobPayload { TaskId = payload.TaskId };
        var executionJob = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(executionPayload),
            Priority = 0,
            Metadata = new Dictionary<string, string> {
                ["TaskId"] = payload.TaskId,
                ["InstallationId"] = payload.InstallationId.ToString(),
                ["RepositoryOwner"] = payload.RepositoryOwner,
                ["RepositoryName"] = payload.RepositoryName,
                ["IssueNumber"] = payload.IssueNumber.ToString()
            }
        };

        var dispatched = await _jobDispatcher.DispatchAsync(executionJob, cancellationToken);
        if (dispatched) {
            _logger.LogInformation("Dispatched execution job {JobId} for task {TaskId}", executionJob.Id, payload.TaskId);
        }
        else {
            _logger.LogWarning("Failed to dispatch execution job for task {TaskId}", payload.TaskId);
        }

        // Update job status to Completed
        var resultData = JsonSerializer.Serialize(new { prNumber, branchName });
        await UpdateJobStatusAsync(
            jobId: job.Id,
            status: BackgroundJobStatus.Completed,
            job.Metadata,
            cancellationToken,
            resultData: resultData);

        return JobResult.CreateSuccess();
    }

    private async Task UpdateJobStatusAsync(
        string jobId,
        BackgroundJobStatus status,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken,
        string? errorMessage = null,
        string? resultData = null) {
        var statusInfo = await _jobStatusStore.GetStatusAsync(jobId, cancellationToken);
        var now = _timeProvider.GetUtcNow().DateTime;
        
        statusInfo = new BackgroundJobStatusInfo {
            JobId = jobId,
            JobType = JobTypeName,
            Status = status,
            CreatedAt = statusInfo?.CreatedAt ?? now,
            StartedAt = status == BackgroundJobStatus.Processing ? now : statusInfo?.StartedAt,
            CompletedAt = status == BackgroundJobStatus.Completed || status == BackgroundJobStatus.Failed || status == BackgroundJobStatus.Cancelled ? now : null,
            ErrorMessage = errorMessage,
            Metadata = metadata,
            ResultData = resultData
        };

        await _jobStatusStore.SetStatusAsync(statusInfo, cancellationToken);
    }

    private static string FormatPrBodyWithPlan(int issueNumber, string issueTitle, string issueBody, AgentPlan plan) {
        var stepsMarkdown = string.Join("\n", plan.Steps.Select(s =>
            $"- [ ] **{EscapeMarkdown(s.Title)}** - {EscapeMarkdown(s.Details)}"));

        var checklistMarkdown = string.Join("\n", plan.Checklist.Select(c =>
            $"- [ ] {EscapeMarkdown(c)}"));

        return $"""
## Plan

**Problem Summary:** {EscapeMarkdown(plan.ProblemSummary)}

### Steps

{stepsMarkdown}

### Checklist

{checklistMarkdown}

### Constraints

{string.Join("\n", plan.Constraints.Select(c => $"- {EscapeMarkdown(c)}"))}

---

<details>
<summary>Original Issue Prompt</summary>

**Issue #{issueNumber}: {EscapeMarkdown(issueTitle)}**

{EscapeMarkdown(issueBody)}

</details>

---

_This PR was automatically created by RG.OpenCopilot._
_Progress will be updated here as the agent works on this issue._
""";
    }

    private static string EscapeMarkdown(string text) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }

        // Escape special markdown characters to prevent injection
        return text
            .Replace("\\", "\\\\")
            .Replace("`", "\\`")
            .Replace("*", "\\*")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace(".", "\\.")
            .Replace("!", "\\!");
    }
}
