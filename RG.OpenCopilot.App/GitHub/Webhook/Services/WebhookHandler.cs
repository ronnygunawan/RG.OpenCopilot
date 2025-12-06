using System.Text.Json;
using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App.GitHub.Webhook.Services;

public interface IWebhookHandler {
    Task HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default);
}

public sealed class WebhookHandler : IWebhookHandler {
    private readonly IAgentTaskStore _taskStore;
    private readonly IPlannerService _plannerService;
    private readonly IGitHubService _gitHubService;
    private readonly IRepositoryAnalyzer _repositoryAnalyzer;
    private readonly IInstructionsLoader _instructionsLoader;
    private readonly ILogger<WebhookHandler> _logger;

    public WebhookHandler(
        IAgentTaskStore taskStore,
        IPlannerService plannerService,
        IGitHubService gitHubService,
        IRepositoryAnalyzer repositoryAnalyzer,
        IInstructionsLoader instructionsLoader,
        ILogger<WebhookHandler> logger) {
        _taskStore = taskStore;
        _plannerService = plannerService;
        _gitHubService = gitHubService;
        _repositoryAnalyzer = repositoryAnalyzer;
        _instructionsLoader = instructionsLoader;
        _logger = logger;
    }

    public async Task HandleIssuesEventAsync(GitHubIssueEventPayload payload, CancellationToken cancellationToken = default) {
        // Check if this is a label event with the copilot-assisted label
        if (payload.Action != "labeled" || payload.Label?.Name != "copilot-assisted") {
            _logger.LogInformation("Ignoring issues event: action={Action}, label={Label}",
                payload.Action, payload.Label?.Name);
            return;
        }

        if (payload.Issue == null || payload.Repository == null || payload.Installation == null) {
            _logger.LogWarning("Received issues event with missing required fields");
            return;
        }

        _logger.LogInformation("Processing copilot-assisted label for issue #{IssueNumber} in {Repo}",
            payload.Issue.Number, payload.Repository.Full_Name);

        try {
            // Create an agent task
            var taskId = $"{payload.Repository.Full_Name}/issues/{payload.Issue.Number}";
            var existingTask = await _taskStore.GetTaskAsync(taskId, cancellationToken);

            if (existingTask != null) {
                _logger.LogInformation("Task {TaskId} already exists, skipping", taskId);
                return;
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

            // Create working branch
            var branchName = await _gitHubService.CreateWorkingBranchAsync(
                task.RepositoryOwner,
                task.RepositoryName,
                task.IssueNumber,
                cancellationToken);

            // Create WIP PR with initial issue prompt
            var prNumber = await _gitHubService.CreateWipPullRequestAsync(
                task.RepositoryOwner,
                task.RepositoryName,
                branchName,
                task.IssueNumber,
                payload.Issue.Title,
                payload.Issue.Body ?? "",
                cancellationToken);

            _logger.LogInformation("Created WIP PR #{PrNumber} for task {TaskId}", prNumber, taskId);

            // Analyze repository to gather context
            RepositoryAnalysis? repoAnalysis = null;
            try {
                repoAnalysis = await _repositoryAnalyzer.AnalyzeAsync(
                    task.RepositoryOwner,
                    task.RepositoryName,
                    cancellationToken);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to analyze repository, proceeding without analysis");
            }

            // Load custom instructions if available
            string? instructions = null;
            try {
                instructions = await _instructionsLoader.LoadInstructionsAsync(
                    task.RepositoryOwner,
                    task.RepositoryName,
                    task.IssueNumber,
                    cancellationToken);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to load instructions, proceeding without them");
            }

            // Generate plan with all available context
            var context = new AgentTaskContext {
                IssueTitle = payload.Issue.Title,
                IssueBody = payload.Issue.Body ?? "",
                RepositorySummary = repoAnalysis?.Summary,
                InstructionsMarkdown = instructions
            };

            var plan = await _plannerService.CreatePlanAsync(context, cancellationToken);
            task.Plan = plan;
            task.Status = AgentTaskStatus.Planned;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);

            _logger.LogInformation("Generated plan for task {TaskId}", taskId);

            // Update PR description with the plan
            var updatedPrBody = FormatPrBodyWithPlan(payload.Issue.Number, payload.Issue.Title, payload.Issue.Body ?? "", plan);
            await _gitHubService.UpdatePullRequestDescriptionAsync(
                task.RepositoryOwner,
                task.RepositoryName,
                prNumber,
                $"[WIP] {payload.Issue.Title}",
                updatedPrBody,
                cancellationToken);

            _logger.LogInformation("Updated PR #{PrNumber} with plan for task {TaskId}", prNumber, taskId);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error handling issues event for issue #{IssueNumber}", payload.Issue.Number);
            throw;
        }
    }

    private static string FormatPrBodyWithPlan(int issueNumber, string issueTitle, string issueBody, AgentPlan plan) {
        var stepsMarkdown = string.Join("\n", plan.Steps.Select(s =>
            $"- [ ] **{EscapeMarkdown(s.Title)}** - {EscapeMarkdown(s.Details)}"));

        var checklistMarkdown = string.Join("\n", plan.Checklist.Select(c =>
            $"- [ ] {EscapeMarkdown(c)}"));

        return $@"## Plan

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
_Progress will be updated here as the agent works on this issue._";
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
