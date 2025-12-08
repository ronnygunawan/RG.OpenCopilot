namespace RG.OpenCopilot.PRGenerationAgent.Execution.Services;

/// <summary>
/// Service for reporting execution progress to pull requests
/// </summary>
public interface IProgressReporter {
    /// <summary>
    /// Reports progress for a completed step execution
    /// </summary>
    Task ReportStepProgressAsync(
        AgentTask task,
        PlanStep step,
        StepExecutionResult result,
        int prNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports overall execution summary with all step results
    /// </summary>
    Task ReportExecutionSummaryAsync(
        AgentTask task,
        List<StepExecutionResult> results,
        int prNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports intermediate progress during long-running operations
    /// </summary>
    Task ReportIntermediateProgressAsync(
        AgentTask task,
        string stage,
        string message,
        int prNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing progress comment
    /// </summary>
    Task UpdateProgressAsync(
        AgentTask task,
        int commentId,
        string updatedContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates PR description to reflect completed steps
    /// </summary>
    Task UpdatePullRequestProgressAsync(
        AgentTask task,
        int prNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts a commit summary comment to the PR
    /// </summary>
    Task ReportCommitSummaryAsync(
        AgentTask task,
        string commitSha,
        string commitMessage,
        List<FileChange> changes,
        int prNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes the PR by removing [WIP] prefix, rewriting description, and archiving WIP details
    /// </summary>
    Task FinalizePullRequestAsync(
        AgentTask task,
        int prNumber,
        CancellationToken cancellationToken = default);
}
