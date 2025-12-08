using System.Text.Json;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Payload for plan execution jobs
/// </summary>
public sealed class ExecutePlanJobPayload {
    public string TaskId { get; init; } = "";
}

/// <summary>
/// Job handler for executing agent plans
/// </summary>
internal sealed class ExecutePlanJobHandler : IJobHandler {
    public const string JobTypeName = "ExecutePlan";
    
    private readonly IAgentTaskStore _taskStore;
    private readonly IExecutorService _executorService;
    private readonly ILogger<ExecutePlanJobHandler> _logger;

    public string JobType => JobTypeName;

    public ExecutePlanJobHandler(
        IAgentTaskStore taskStore,
        IExecutorService executorService,
        ILogger<ExecutePlanJobHandler> logger) {
        _taskStore = taskStore;
        _executorService = executorService;
        _logger = logger;
    }

    public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
        try {
            // Deserialize payload
            var payload = JsonSerializer.Deserialize<ExecutePlanJobPayload>(job.Payload);
            if (payload == null) {
                return JobResult.CreateFailure("Failed to deserialize job payload", shouldRetry: false);
            }

            // Get task
            var task = await _taskStore.GetTaskAsync(payload.TaskId, cancellationToken);
            if (task == null) {
                return JobResult.CreateFailure($"Task {payload.TaskId} not found", shouldRetry: false);
            }

            if (task.Plan == null) {
                return JobResult.CreateFailure($"Task {payload.TaskId} has no plan", shouldRetry: false);
            }

            _logger.LogInformation("Executing plan for task {TaskId}", payload.TaskId);

            // Update task status
            task.Status = AgentTaskStatus.Executing;
            task.StartedAt = DateTime.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);

            // Execute plan
            await _executorService.ExecutePlanAsync(task, cancellationToken);

            // Update task status
            task.Status = AgentTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);

            _logger.LogInformation("Successfully executed plan for task {TaskId}", payload.TaskId);

            return JobResult.CreateSuccess();
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Plan execution cancelled for job {JobId}", job.Id);
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to execute plan for job {JobId}", job.Id);
            return JobResult.CreateFailure(ex.Message, exception: ex, shouldRetry: true);
        }
    }
}
