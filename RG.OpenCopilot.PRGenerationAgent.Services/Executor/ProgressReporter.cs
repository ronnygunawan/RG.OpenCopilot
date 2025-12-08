using System.Text;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Git.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Executor;

/// <summary>
/// Reports execution progress to pull request comments
/// </summary>
internal sealed class ProgressReporter : IProgressReporter {
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<ProgressReporter> _logger;

    public ProgressReporter(
        IGitHubService gitHubService,
        ILogger<ProgressReporter> logger) {
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task ReportStepProgressAsync(
        AgentTask task,
        PlanStep step,
        StepExecutionResult result,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        var comment = FormatProgressComment(step, result, task);
        
        await _gitHubService.PostPullRequestCommentAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            comment: comment,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Posted step progress comment for step '{StepTitle}' to PR #{PrNumber}",
            step.Title,
            prNumber);
    }

    public async Task ReportExecutionSummaryAsync(
        AgentTask task,
        List<StepExecutionResult> results,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        var comment = FormatSummaryComment(task, results);
        
        await _gitHubService.PostPullRequestCommentAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            comment: comment,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Posted execution summary comment to PR #{PrNumber}",
            prNumber);
    }

    public async Task ReportIntermediateProgressAsync(
        AgentTask task,
        string stage,
        string message,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        var comment = FormatIntermediateProgress(stage, message);
        
        await _gitHubService.PostPullRequestCommentAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            comment: comment,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Posted intermediate progress comment for stage '{Stage}' to PR #{PrNumber}",
            stage,
            prNumber);
    }

    public Task UpdateProgressAsync(
        AgentTask task,
        int commentId,
        string updatedContent,
        CancellationToken cancellationToken = default) {
        // Note: GitHub API doesn't support updating comments directly via Octokit IGitHubIssueAdapter
        // This would require extending the adapter with UpdateCommentAsync method
        // For now, we post new comments instead of updating existing ones
        _logger.LogWarning(
            "UpdateProgressAsync called but comment updates are not yet implemented. CommentId: {CommentId}",
            commentId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Formats a progress comment for a completed step
    /// </summary>
    private string FormatProgressComment(PlanStep step, StepExecutionResult result, AgentTask task) {
        var sb = new StringBuilder();
        
        sb.AppendLine($"## 📝 Step Progress: {step.Title}");
        sb.AppendLine();
        
        // Status header
        var statusIcon = result.Success ? "✅" : "❌";
        var statusText = result.Success ? "Completed Successfully" : "Failed";
        sb.AppendLine($"### {statusIcon} Status: {statusText}");
        sb.AppendLine();
        
        // Duration and timing info
        sb.AppendLine($"**Duration:** {FormatDuration(result.Duration)}");
        
        if (task.StartedAt.HasValue) {
            var elapsed = DateTime.UtcNow - task.StartedAt.Value;
            sb.AppendLine($"**Elapsed Time:** {FormatDuration(elapsed)}");
            
            // Calculate estimated completion
            if (task.Plan != null) {
                var totalSteps = task.Plan.Steps.Count;
                var completedSteps = task.Plan.Steps.Count(s => s.Done);
                
                if (completedSteps > 0 && completedSteps < totalSteps) {
                    var avgTimePerStep = elapsed.TotalSeconds / completedSteps;
                    var remainingSteps = totalSteps - completedSteps;
                    var estimatedRemaining = TimeSpan.FromSeconds(avgTimePerStep * remainingSteps);
                    
                    sb.AppendLine($"**Estimated Completion:** {FormatDuration(estimatedRemaining)} remaining");
                    sb.AppendLine($"**Progress:** {completedSteps}/{totalSteps} steps completed");
                }
            }
        }
        
        sb.AppendLine();
        
        // Error details if failed
        if (!result.Success && !string.IsNullOrEmpty(result.Error)) {
            sb.AppendLine("### ⚠️ Error Details");
            sb.AppendLine("```");
            sb.AppendLine(result.Error);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        
        // Changes made
        if (result.Changes.Count > 0) {
            sb.AppendLine("### 📂 Changes Made");
            foreach (var change in result.Changes) {
                var changeIcon = change.Type switch {
                    ChangeType.Created => "✨",
                    ChangeType.Modified => "✏️",
                    ChangeType.Deleted => "🗑️",
                    _ => "📄"
                };
                sb.AppendLine($"- {changeIcon} {change.Type}: `{change.Path}`");
            }
            sb.AppendLine();
        }
        
        // Build results
        if (result.BuildResult != null) {
            sb.AppendLine("### 🔨 Build Results");
            var buildIcon = result.BuildResult.Success ? "✅" : "❌";
            sb.AppendLine($"- {buildIcon} Build {(result.BuildResult.Success ? "succeeded" : "failed")} on attempt {result.BuildResult.Attempts}");
            sb.AppendLine($"- Duration: {FormatDuration(result.BuildResult.Duration)}");
            
            if (result.BuildResult.Errors.Count > 0) {
                sb.AppendLine($"- Errors: {result.BuildResult.Errors.Count}");
            }
            
            if (result.BuildResult.FixesApplied.Count > 0) {
                sb.AppendLine($"- Fixes applied: {result.BuildResult.FixesApplied.Count}");
            }
            sb.AppendLine();
        }
        
        // Test results
        if (result.TestResult != null) {
            sb.AppendLine("### 🧪 Test Results");
            var testIcon = result.TestResult.AllPassed ? "✅" : "⚠️";
            sb.AppendLine($"- {testIcon} {(result.TestResult.AllPassed ? "All tests passed" : "Some tests failed")} ({result.TestResult.PassedTests}/{result.TestResult.TotalTests})");
            sb.AppendLine($"- Duration: {FormatDuration(result.TestResult.Duration)}");
            
            if (result.TestResult.FailedTests > 0) {
                sb.AppendLine($"- Failed: {result.TestResult.FailedTests}");
            }
            
            if (result.TestResult.SkippedTests > 0) {
                sb.AppendLine($"- Skipped: {result.TestResult.SkippedTests}");
            }
            
            if (result.TestResult.FixesApplied.Count > 0) {
                sb.AppendLine($"- Fixes applied: {result.TestResult.FixesApplied.Count}");
            }
            sb.AppendLine();
        }
        
        // Execution metrics
        if (result.Metrics != null) {
            sb.AppendLine("### 📊 Execution Metrics");
            
            if (result.Metrics.LLMCalls > 0) {
                sb.AppendLine($"- LLM calls: {result.Metrics.LLMCalls}");
            }
            
            var filesAnalyzed = result.Metrics.FilesCreated + result.Metrics.FilesModified + result.Metrics.FilesDeleted;
            if (filesAnalyzed > 0) {
                sb.AppendLine($"- Files analyzed: {filesAnalyzed}");
            }
            
            if (result.Metrics.CodeGenerationDuration > TimeSpan.Zero) {
                sb.AppendLine($"- Code generation time: {FormatDuration(result.Metrics.CodeGenerationDuration)}");
            }
            
            if (result.Metrics.AnalysisDuration > TimeSpan.Zero) {
                sb.AppendLine($"- Analysis time: {FormatDuration(result.Metrics.AnalysisDuration)}");
            }
            sb.AppendLine();
        }
        
        // Footer
        sb.AppendLine("---");
        sb.AppendLine($"_Automated update by RG.OpenCopilot • Step: {step.Title}_");
        
        return sb.ToString();
    }

    /// <summary>
    /// Formats an execution summary comment
    /// </summary>
    private string FormatSummaryComment(AgentTask task, List<StepExecutionResult> results) {
        var sb = new StringBuilder();
        
        sb.AppendLine("## 🎯 Execution Summary");
        sb.AppendLine();
        
        var totalDuration = results.Sum(r => r.Duration.TotalSeconds);
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count - successCount;
        
        sb.AppendLine($"**Total Steps:** {results.Count}");
        sb.AppendLine($"**Successful:** {successCount}");
        sb.AppendLine($"**Failed:** {failureCount}");
        sb.AppendLine($"**Total Duration:** {FormatDuration(TimeSpan.FromSeconds(totalDuration))}");
        sb.AppendLine();
        
        // Step breakdown
        sb.AppendLine("### 📋 Step Breakdown");
        for (int i = 0; i < results.Count; i++) {
            var result = results[i];
            var statusIcon = result.Success ? "✅" : "❌";
            var stepNumber = i + 1;
            sb.AppendLine($"{stepNumber}. {statusIcon} {(result.Success ? "Success" : "Failed")} - {FormatDuration(result.Duration)}");
        }
        sb.AppendLine();
        
        // Aggregate metrics
        var totalMetrics = new ExecutionMetrics {
            FilesCreated = results.Sum(r => r.Metrics.FilesCreated),
            FilesModified = results.Sum(r => r.Metrics.FilesModified),
            FilesDeleted = results.Sum(r => r.Metrics.FilesDeleted),
            LLMCalls = results.Sum(r => r.Metrics.LLMCalls),
            BuildAttempts = results.Sum(r => r.Metrics.BuildAttempts),
            TestAttempts = results.Sum(r => r.Metrics.TestAttempts),
            AnalysisDuration = TimeSpan.FromSeconds(results.Sum(r => r.Metrics.AnalysisDuration.TotalSeconds)),
            CodeGenerationDuration = TimeSpan.FromSeconds(results.Sum(r => r.Metrics.CodeGenerationDuration.TotalSeconds)),
            BuildDuration = TimeSpan.FromSeconds(results.Sum(r => r.Metrics.BuildDuration.TotalSeconds)),
            TestDuration = TimeSpan.FromSeconds(results.Sum(r => r.Metrics.TestDuration.TotalSeconds))
        };
        
        sb.AppendLine("### 📊 Overall Metrics");
        sb.AppendLine($"- Total files created: {totalMetrics.FilesCreated}");
        sb.AppendLine($"- Total files modified: {totalMetrics.FilesModified}");
        sb.AppendLine($"- Total files deleted: {totalMetrics.FilesDeleted}");
        sb.AppendLine($"- Total LLM calls: {totalMetrics.LLMCalls}");
        sb.AppendLine($"- Total build attempts: {totalMetrics.BuildAttempts}");
        sb.AppendLine($"- Total test attempts: {totalMetrics.TestAttempts}");
        sb.AppendLine();
        
        // Time breakdown
        sb.AppendLine("### ⏱️ Time Breakdown");
        sb.AppendLine($"- Analysis: {FormatDuration(totalMetrics.AnalysisDuration)}");
        sb.AppendLine($"- Code generation: {FormatDuration(totalMetrics.CodeGenerationDuration)}");
        sb.AppendLine($"- Building: {FormatDuration(totalMetrics.BuildDuration)}");
        sb.AppendLine($"- Testing: {FormatDuration(totalMetrics.TestDuration)}");
        sb.AppendLine();
        
        // Footer
        sb.AppendLine("---");
        sb.AppendLine("_Automated execution summary by RG.OpenCopilot_");
        
        return sb.ToString();
    }

    /// <summary>
    /// Formats an intermediate progress update
    /// </summary>
    private string FormatIntermediateProgress(string stage, string message) {
        var sb = new StringBuilder();
        
        sb.AppendLine($"## 🔄 In Progress: {stage}");
        sb.AppendLine();
        sb.AppendLine(message);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"_Automated update by RG.OpenCopilot • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC_");
        
        return sb.ToString();
    }

    /// <summary>
    /// Formats a duration in a human-readable format
    /// </summary>
    private string FormatDuration(TimeSpan duration) {
        if (duration.TotalMinutes < 1) {
            return $"{duration.TotalSeconds:F0}s";
        }
        
        if (duration.TotalHours < 1) {
            return $"{duration.Minutes}m {duration.Seconds}s";
        }
        
        return $"{duration.Hours}h {duration.Minutes}m {duration.Seconds}s";
    }

    public async Task UpdatePullRequestProgressAsync(
        AgentTask task,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        if (task.Plan == null) {
            _logger.LogWarning("Cannot update PR progress for task {TaskId} - no plan available", task.Id);
            return;
        }

        // Get current PR to preserve its content
        var pr = await _gitHubService.GetPullRequestAsync(
            task.RepositoryOwner,
            task.RepositoryName,
            prNumber,
            cancellationToken);

        // Update the PR body with checked-off steps
        var updatedBody = UpdatePrBodyWithProgress(pr.Body, task.Plan);

        await _gitHubService.UpdatePullRequestDescriptionAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            title: pr.Title,
            body: updatedBody,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Updated PR #{PrNumber} description with progress for task {TaskId}",
            prNumber,
            task.Id);
    }

    public async Task ReportCommitSummaryAsync(
        AgentTask task,
        string commitSha,
        string commitMessage,
        List<FileChange> changes,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        var comment = FormatCommitSummary(commitSha, commitMessage, changes);
        
        await _gitHubService.PostPullRequestCommentAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            comment: comment,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Posted commit summary comment for {CommitSha} to PR #{PrNumber}",
            TruncateCommitSha(commitSha),
            prNumber);
    }

    /// <summary>
    /// Truncates commit SHA to specified length for display
    /// </summary>
    private static string TruncateCommitSha(string commitSha, int length = 7) {
        return commitSha.Length <= length ? commitSha : commitSha[..length];
    }

    /// <summary>
    /// Updates PR body to check off completed steps
    /// </summary>
    private string UpdatePrBodyWithProgress(string currentBody, AgentPlan plan) {
        var lines = currentBody.Split('\n');
        var updatedLines = new List<string>();
        var inStepsSection = false;

        foreach (var line in lines) {
            // Detect steps section
            if (line.TrimStart().StartsWith("### Steps")) {
                inStepsSection = true;
                updatedLines.Add(line);
                continue;
            }

            // Detect end of steps section
            if (inStepsSection && (line.TrimStart().StartsWith("### ") || line.TrimStart().StartsWith("## "))) {
                inStepsSection = false;
            }

            // Update checkboxes in steps section
            if (inStepsSection && line.TrimStart().StartsWith("- [ ] ")) {
                var stepTitle = ExtractStepTitle(line);
                var isDone = plan.Steps.Any(s => s.Title == stepTitle && s.Done);
                
                if (isDone) {
                    // Check off the box
                    var updatedLine = line.Replace("- [ ] ", "- [x] ");
                    updatedLines.Add(updatedLine);
                } else {
                    updatedLines.Add(line);
                }
            } else {
                updatedLines.Add(line);
            }
        }

        return string.Join('\n', updatedLines);
    }

    /// <summary>
    /// Extracts the step title from a checklist line
    /// </summary>
    private string ExtractStepTitle(string checklistLine) {
        // Remove checkbox and extract title (between ** markers)
        var cleaned = checklistLine.Replace("- [ ] ", "").Replace("- [x] ", "").Trim();
        
        // Extract text between ** markers
        var startIdx = cleaned.IndexOf("**");
        if (startIdx == -1) return cleaned;
        
        var endIdx = cleaned.IndexOf("**", startIdx + 2);
        if (endIdx == -1) return cleaned;
        
        var title = cleaned.Substring(startIdx + 2, endIdx - startIdx - 2);
        
        // Unescape markdown
        return UnescapeMarkdown(title);
    }

    /// <summary>
    /// Unescapes markdown characters
    /// </summary>
    private string UnescapeMarkdown(string text) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }

        return text
            .Replace("\\\\", "\\")
            .Replace("\\`", "`")
            .Replace("\\*", "*")
            .Replace("\\_", "_")
            .Replace("\\{", "{")
            .Replace("\\}", "}")
            .Replace("\\[", "[")
            .Replace("\\]", "]")
            .Replace("\\(", "(")
            .Replace("\\)", ")")
            .Replace("\\#", "#")
            .Replace("\\+", "+")
            .Replace("\\-", "-")
            .Replace("\\.", ".")
            .Replace("\\!", "!");
    }

    /// <summary>
    /// Formats a commit summary comment
    /// </summary>
    private string FormatCommitSummary(string commitSha, string commitMessage, List<FileChange> changes) {
        var sb = new StringBuilder();
        
        sb.AppendLine($"## 📦 Commit Summary");
        sb.AppendLine();
        sb.AppendLine($"**Commit:** `{TruncateCommitSha(commitSha)}`");
        sb.AppendLine($"**Message:** {commitMessage}");
        sb.AppendLine();
        
        if (changes.Count > 0) {
            sb.AppendLine("### 📂 Files Changed");
            
            var created = changes.Where(c => c.Type == ChangeType.Created).ToList();
            var modified = changes.Where(c => c.Type == ChangeType.Modified).ToList();
            var deleted = changes.Where(c => c.Type == ChangeType.Deleted).ToList();
            
            if (created.Count > 0) {
                sb.AppendLine();
                sb.AppendLine($"**Created ({created.Count}):**");
                foreach (var change in created) {
                    sb.AppendLine($"- ✨ `{change.Path}`");
                }
            }
            
            if (modified.Count > 0) {
                sb.AppendLine();
                sb.AppendLine($"**Modified ({modified.Count}):**");
                foreach (var change in modified) {
                    sb.AppendLine($"- ✏️ `{change.Path}`");
                }
            }
            
            if (deleted.Count > 0) {
                sb.AppendLine();
                sb.AppendLine($"**Deleted ({deleted.Count}):**");
                foreach (var change in deleted) {
                    sb.AppendLine($"- 🗑️ `{change.Path}`");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine($"**Total:** {changes.Count} file(s) changed");
        } else {
            sb.AppendLine("_No file changes in this commit_");
        }
        
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("_Automated commit summary by RG.OpenCopilot_");
        
        return sb.ToString();
    }

    public async Task FinalizePullRequestAsync(
        AgentTask task,
        int prNumber,
        CancellationToken cancellationToken = default) {
        
        if (task.Plan == null) {
            _logger.LogWarning("Cannot finalize PR for task {TaskId} - no plan available", task.Id);
            return;
        }

        // Get current PR to extract content
        var pr = await _gitHubService.GetPullRequestAsync(
            task.RepositoryOwner,
            task.RepositoryName,
            prNumber,
            cancellationToken);

        // Remove [WIP] prefix from title
        var finalTitle = pr.Title.Replace("[WIP] ", "").Trim();

        // Create reviewer-friendly description
        var finalDescription = FormatFinalPrDescription(task, pr.Body);

        // Update the PR
        await _gitHubService.UpdatePullRequestDescriptionAsync(
            owner: task.RepositoryOwner,
            repo: task.RepositoryName,
            prNumber: prNumber,
            title: finalTitle,
            body: finalDescription,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Finalized PR #{PrNumber} for task {TaskId} - removed [WIP] prefix and archived WIP details",
            prNumber,
            task.Id);
    }

    /// <summary>
    /// Formats the final PR description with WIP details archived in a collapsed section
    /// </summary>
    private string FormatFinalPrDescription(AgentTask task, string wipBody) {
        var sb = new StringBuilder();
        
        // Create reviewer-friendly summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"This PR implements the changes requested in issue #{task.IssueNumber}.");
        sb.AppendLine();
        
        if (task.Plan != null) {
            sb.AppendLine("### Changes Made");
            sb.AppendLine();
            
            foreach (var step in task.Plan.Steps.Where(s => s.Done)) {
                sb.AppendLine($"- ✅ {step.Title}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("### Testing");
        sb.AppendLine();
        sb.AppendLine("All automated tests have been run and verified to pass.");
        sb.AppendLine();
        
        // Archive WIP details in collapsed section
        sb.AppendLine("<details>");
        sb.AppendLine("<summary>📋 WIP Progress Details (Click to expand)</summary>");
        sb.AppendLine();
        sb.AppendLine(wipBody);
        sb.AppendLine();
        sb.AppendLine("</details>");
        sb.AppendLine();
        
        sb.AppendLine("---");
        sb.AppendLine("_This PR was automatically created and completed by RG.OpenCopilot_");
        
        return sb.ToString();
    }
}
