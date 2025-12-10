using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

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
    private readonly BackgroundJobOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExecutePlanJobHandler> _logger;

    public string JobType => JobTypeName;

    public ExecutePlanJobHandler(
        IAgentTaskStore taskStore,
        IExecutorService executorService,
        BackgroundJobOptions options,
        TimeProvider timeProvider,
        ILogger<ExecutePlanJobHandler> logger) {
        _taskStore = taskStore;
        _executorService = executorService;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
        try {
            // Apply timeout if configured
            CancellationTokenSource? timeoutCts = null;
            CancellationToken effectiveCancellationToken = cancellationToken;

            if (_options.ExecutionTimeoutSeconds > 0) {
                timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));
                effectiveCancellationToken = timeoutCts.Token;
                _logger.LogInformation("Plan execution timeout set to {TimeoutSeconds} seconds", _options.ExecutionTimeoutSeconds);
            }

            try {
                return await ExecutePlanInternalAsync(job, effectiveCancellationToken);
            }
            finally {
                timeoutCts?.Dispose();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            // Timeout occurred
            _logger.LogWarning("Plan execution timed out for job {JobId} after {TimeoutSeconds} seconds", 
                job.Id, _options.ExecutionTimeoutSeconds);
            
            // Update task status
            var payload = JsonSerializer.Deserialize<ExecutePlanJobPayload>(job.Payload);
            if (payload != null) {
                var task = await _taskStore.GetTaskAsync(payload.TaskId, cancellationToken);
                if (task != null) {
                    task.Status = AgentTaskStatus.Failed;
                    task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
                    await _taskStore.UpdateTaskAsync(task, cancellationToken);
                }
            }
            
            return JobResult.CreateFailure(
                errorMessage: $"Plan execution timed out after {_options.ExecutionTimeoutSeconds} seconds",
                shouldRetry: false);
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Plan execution cancelled for job {JobId}", job.Id);
            
            // Update task status
            var payload = JsonSerializer.Deserialize<ExecutePlanJobPayload>(job.Payload);
            if (payload != null) {
                var task = await _taskStore.GetTaskAsync(payload.TaskId, cancellationToken);
                if (task != null) {
                    task.Status = AgentTaskStatus.Cancelled;
                    task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
                    await _taskStore.UpdateTaskAsync(task, cancellationToken);
                }
            }
            
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to execute plan for job {JobId}", job.Id);
            return JobResult.CreateFailure(ex.Message, exception: ex, shouldRetry: true);
        }
    }

    private async Task<JobResult> ExecutePlanInternalAsync(BackgroundJob job, CancellationToken cancellationToken) {
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
        task.StartedAt = _timeProvider.GetUtcNow().DateTime;
        await _taskStore.UpdateTaskAsync(task, cancellationToken);

        // Execute plan
        await _executorService.ExecutePlanAsync(task, cancellationToken);

        // Update task status
        task.Status = AgentTaskStatus.Completed;
        task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
        await _taskStore.UpdateTaskAsync(task, cancellationToken);

        _logger.LogInformation("Successfully executed plan for task {TaskId}", payload.TaskId);

        return JobResult.CreateSuccess();
    }
}
