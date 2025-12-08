# Background Job Processing Documentation

## Overview

The RG.OpenCopilot background job processing infrastructure provides a flexible, extensible system for executing long-running tasks asynchronously. It uses a channel-based queue with priority support and can process multiple jobs concurrently with configurable limits.

## Architecture

The background job processing system consists of four main components:

1. **Job Queue** (`IJobQueue`/`ChannelJobQueue`): Thread-safe queue for enqueuing and dequeuing jobs
2. **Job Dispatcher** (`IJobDispatcher`/`JobDispatcher`): Manages job handlers and dispatches jobs to the queue
3. **Job Handler** (`IJobHandler`): Processes specific types of jobs
4. **Background Job Processor** (`BackgroundJobProcessor`): Background service that dequeues and executes jobs

## Configuration

Background job processing is configured in `appsettings.json`:

```json
{
  "BackgroundJobs": {
    "MaxConcurrency": 2,
    "MaxQueueSize": 100,
    "EnablePrioritization": true,
    "ShutdownTimeoutSeconds": 30,
    "EnableRetry": true,
    "RetryDelayMilliseconds": 5000
  }
}
```

### Configuration Options

- **MaxConcurrency**: Maximum number of jobs to process concurrently (default: 2)
- **MaxQueueSize**: Maximum number of jobs in the queue, 0 for unlimited (default: 100)
- **EnablePrioritization**: Whether to process higher priority jobs first (default: true)
- **ShutdownTimeoutSeconds**: Time to wait for jobs to complete during shutdown (default: 30)
- **EnableRetry**: Whether to automatically retry failed jobs (default: true)
- **RetryDelayMilliseconds**: Delay between retry attempts (default: 5000)

## Adding New Job Types

To add a new job type, follow these steps:

### 1. Define Your Job Payload

Create a class to represent your job's payload. This will be serialized to JSON and stored in the `BackgroundJob.Payload` property.

```csharp
public sealed class MyCustomJobPayload {
    public string TaskId { get; init; } = "";
    public string SomeParameter { get; init; } = "";
    public int AnotherParameter { get; init; }
}
```

### 2. Implement IJobHandler

Create a job handler that implements `IJobHandler`:

```csharp
using System.Text.Json;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

internal sealed class MyCustomJobHandler : IJobHandler {
    private readonly ILogger<MyCustomJobHandler> _logger;
    // Inject any services you need
    private readonly IMyService _myService;

    // Define the job type identifier
    public string JobType => "MyCustomJob";

    public MyCustomJobHandler(
        IMyService myService,
        ILogger<MyCustomJobHandler> logger) {
        _myService = myService;
        _logger = logger;
    }

    public async Task<JobResult> ExecuteAsync(
        BackgroundJob job,
        CancellationToken cancellationToken = default) {
        try {
            // Deserialize the payload
            var payload = JsonSerializer.Deserialize<MyCustomJobPayload>(job.Payload);
            if (payload == null) {
                return JobResult.CreateFailure(
                    errorMessage: "Failed to deserialize job payload",
                    shouldRetry: false);
            }

            _logger.LogInformation(
                "Executing MyCustomJob for task {TaskId}",
                payload.TaskId);

            // Do your work here
            await _myService.DoSomethingAsync(
                payload.TaskId,
                payload.SomeParameter,
                cancellationToken);

            _logger.LogInformation(
                "Successfully completed MyCustomJob for task {TaskId}",
                payload.TaskId);

            return JobResult.CreateSuccess();
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Job {JobId} was cancelled", job.Id);
            throw; // Re-throw to allow proper cancellation handling
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to execute job {JobId}", job.Id);
            return JobResult.CreateFailure(
                errorMessage: ex.Message,
                exception: ex,
                shouldRetry: true); // Set to true if job should be retried
        }
    }
}
```

### 3. Register Your Handler

Register your handler in `ServiceCollectionExtensions.cs`:

```csharp
// In AddPRGenerationAgentServices method, add:
services.AddSingleton<IJobHandler, MyCustomJobHandler>();
```

The handler will be automatically registered with the dispatcher during service configuration.

### 4. Dispatch Jobs

To dispatch a job from anywhere in your code:

```csharp
public class MyService {
    private readonly IJobDispatcher _jobDispatcher;

    public MyService(IJobDispatcher jobDispatcher) {
        _jobDispatcher = jobDispatcher;
    }

    public async Task TriggerMyJobAsync(string taskId, string param) {
        var payload = new MyCustomJobPayload {
            TaskId = taskId,
            SomeParameter = param,
            AnotherParameter = 42
        };

        var job = new BackgroundJob {
            Type = "MyCustomJob", // Must match JobHandler.JobType
            Payload = JsonSerializer.Serialize(payload),
            Priority = 5, // Higher values processed first (0-10 typical range)
            MaxRetries = 3, // Number of retry attempts if job fails
            Metadata = new Dictionary<string, string> {
                ["TaskId"] = taskId,
                ["Source"] = "MyService"
            }
        };

        var dispatched = await _jobDispatcher.DispatchAsync(job);
        if (!dispatched) {
            // Handle dispatch failure
            throw new InvalidOperationException("Failed to dispatch job");
        }
    }
}
```

## Job Lifecycle

1. **Dispatch**: Job is created and dispatched via `IJobDispatcher.DispatchAsync()`
2. **Enqueue**: Job is added to the priority queue
3. **Dequeue**: Background processor retrieves the next job from the queue
4. **Execute**: Appropriate handler executes the job
5. **Complete/Retry**: Job completes successfully or is retried if it failed and retry is enabled

## Error Handling and Retries

Jobs can fail and be retried automatically if configured:

- Set `shouldRetry: true` in `JobResult.CreateFailure()` to enable retry
- Configure `MaxRetries` on the job (default: 3)
- Configure `RetryDelayMilliseconds` in settings (default: 5000ms)
- Each retry increments `job.RetryCount`

## Cancellation

Jobs can be cancelled using the job dispatcher:

```csharp
bool cancelled = _jobDispatcher.CancelJob("job-id");
```

Job handlers should check the `cancellationToken` and throw `OperationCanceledException` when cancelled.

## Thread Safety

All components are thread-safe:

- `ChannelJobQueue` uses thread-safe channels
- `JobDispatcher` uses `ConcurrentDictionary` for handler and job tracking
- `BackgroundJobProcessor` uses semaphores for concurrency control

## Testing

Example test for a custom job handler:

```csharp
[Fact]
public async Task MyCustomJobHandler_ExecutesSuccessfully() {
    // Arrange
    var myService = new Mock<IMyService>();
    var logger = new TestLogger<MyCustomJobHandler>();
    var handler = new MyCustomJobHandler(myService.Object, logger);

    myService
        .Setup(s => s.DoSomethingAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var job = new BackgroundJob {
        Type = "MyCustomJob",
        Payload = """{"TaskId":"test-id","SomeParameter":"value","AnotherParameter":42}"""
    };

    // Act
    var result = await handler.ExecuteAsync(job);

    // Assert
    result.Success.ShouldBeTrue();
    myService.Verify(
        s => s.DoSomethingAsync("test-id", "value", It.IsAny<CancellationToken>()),
        Times.Once);
}
```

## Best Practices

1. **Keep handlers focused**: Each handler should do one thing well
2. **Use appropriate logging**: Log start, success, failure, and key milestones
3. **Handle cancellation**: Always check cancellation tokens for long-running operations
4. **Set retry appropriately**: Use `shouldRetry: true` for transient failures, `false` for permanent failures
5. **Include metadata**: Add useful metadata to jobs for debugging and monitoring
6. **Validate payloads**: Always validate deserialized payloads before processing
7. **Use dependency injection**: Inject services into handlers rather than creating them directly
8. **Test thoroughly**: Test happy path, failure cases, and cancellation scenarios

## Example: ExecutePlan Job Handler

The built-in `ExecutePlanJobHandler` demonstrates these patterns:

```csharp
internal sealed class ExecutePlanJobHandler : IJobHandler {
    public string JobType => "ExecutePlan";

    public async Task<JobResult> ExecuteAsync(
        BackgroundJob job,
        CancellationToken cancellationToken = default) {
        try {
            var payload = JsonSerializer.Deserialize<ExecutePlanJobPayload>(job.Payload);
            if (payload == null) {
                return JobResult.CreateFailure("Failed to deserialize job payload", shouldRetry: false);
            }

            var task = await _taskStore.GetTaskAsync(payload.TaskId, cancellationToken);
            if (task == null) {
                return JobResult.CreateFailure($"Task {payload.TaskId} not found", shouldRetry: false);
            }

            if (task.Plan == null) {
                return JobResult.CreateFailure($"Task {payload.TaskId} has no plan", shouldRetry: false);
            }

            task.Status = AgentTaskStatus.Executing;
            task.StartedAt = DateTime.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);

            await _executorService.ExecutePlanAsync(task, cancellationToken);

            task.Status = AgentTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);

            return JobResult.CreateSuccess();
        }
        catch (OperationCanceledException) {
            throw; // Re-throw for proper handling
        }
        catch (Exception ex) {
            return JobResult.CreateFailure(ex.Message, exception: ex, shouldRetry: true);
        }
    }
}
```
