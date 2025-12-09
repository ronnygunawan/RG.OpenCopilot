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
- **EnableRetry**: (Deprecated) Whether to automatically retry failed jobs (default: true). Use RetryPolicy instead.
- **RetryDelayMilliseconds**: (Deprecated) Delay between retry attempts (default: 5000). Use RetryPolicy instead.

### Advanced Retry Configuration

For more control over retry behavior, configure the retry policy with backoff strategies and jitter:

```json
{
  "BackgroundJobs": {
    "MaxConcurrency": 2,
    "MaxQueueSize": 100,
    "EnablePrioritization": true,
    "ShutdownTimeoutSeconds": 30,
    "RetryPolicy": {
      "Enabled": true,
      "MaxRetries": 3,
      "BackoffStrategy": "Exponential",
      "BaseDelayMilliseconds": 5000,
      "MaxDelayMilliseconds": 300000,
      "MinJitterFactor": 0.0,
      "MaxJitterFactor": 0.2
    }
  }
}
```

#### Retry Policy Options

- **Enabled**: Whether to retry failed jobs (default: true)
- **MaxRetries**: Maximum number of retry attempts (default: 3)
- **BackoffStrategy**: Strategy for calculating retry delays. Options:
  - `Constant`: Fixed delay between retries (uses BaseDelayMilliseconds)
  - `Linear`: Linearly increasing delay (baseDelay × (retryCount + 1))
  - `Exponential`: Exponentially increasing delay (baseDelay × 2^retryCount) - **recommended**
- **BaseDelayMilliseconds**: Base delay for retry calculations (default: 5000ms)
- **MaxDelayMilliseconds**: Maximum delay cap to prevent exponential explosion (default: 300000ms = 5 minutes)
- **MinJitterFactor**: Minimum random jitter as a percentage (-0.1 = reduce by up to 10%)
- **MaxJitterFactor**: Maximum random jitter as a percentage (0.2 = increase by up to 20%)

#### Backoff Strategy Examples

**Constant Backoff:**
```
Retry 1: 5000ms
Retry 2: 5000ms
Retry 3: 5000ms
```

**Linear Backoff (BaseDelay = 5000ms):**
```
Retry 1: 5000ms  (5000 × 1)
Retry 2: 10000ms (5000 × 2)
Retry 3: 15000ms (5000 × 3)
```

**Exponential Backoff (BaseDelay = 5000ms):**
```
Retry 1: 5000ms  (5000 × 2^0)
Retry 2: 10000ms (5000 × 2^1)
Retry 3: 20000ms (5000 × 2^2)
Retry 4: 40000ms (5000 × 2^3)
```

With jitter (MinJitter = -0.1, MaxJitter = 0.2), delays will vary from -10% to +20% to prevent thundering herd problems.

## Idempotency and Deduplication

Background jobs support idempotency keys to prevent duplicate execution of the same logical operation. When a job is dispatched with an idempotency key, the system ensures that only one job with that key can be in-flight at any time.

### Using Idempotency Keys

```csharp
var payload = new MyJobPayload {
    TaskId = "task-123",
    Operation = "process-data"
};

var job = new BackgroundJob {
    Type = "MyJob",
    Payload = JsonSerializer.Serialize(payload),
    IdempotencyKey = $"MyJob:{payload.TaskId}:{payload.Operation}",  // Unique identifier
    Metadata = new Dictionary<string, string> {
        ["TaskId"] = payload.TaskId
    }
};

var dispatched = await _jobDispatcher.DispatchAsync(job);
if (!dispatched) {
    // Job with this idempotency key is already in-flight
    _logger.LogWarning("Duplicate job detected, skipping dispatch");
}
```

### Idempotency Key Best Practices

1. **Make keys unique but consistent**: Include all parameters that define the unique operation
2. **Use descriptive prefixes**: Start with job type to avoid collisions across different job types
3. **Include version if needed**: `MyJob:v2:task-123` for versioned operations
4. **Clean format**: Use colons (`:`) or hyphens (`-`) as separators
5. **Avoid timestamps**: Don't include timestamps or random values that change between retries

**Good Examples:**
```csharp
IdempotencyKey = $"GeneratePlan:{owner}/{repo}/issues/{issueNumber}"
IdempotencyKey = $"ProcessPayment:{orderId}:{userId}"
IdempotencyKey = $"SendEmail:{templateId}:{recipientEmail}:{timestamp.Date}"
```

**Bad Examples:**
```csharp
IdempotencyKey = Guid.NewGuid().ToString()  // Changes every time!
IdempotencyKey = $"job-{DateTime.Now.Ticks}"  // Not idempotent!
```

## Attempt History and Dead-Letter Queue

All job execution attempts are tracked and stored, providing full visibility into retry history. When a job exceeds its maximum retry attempts, it's moved to the dead-letter queue with complete attempt history.

### Attempt Tracking

Each attempt records:
- Attempt number (1-based)
- Start and completion timestamps
- Success/failure status
- Error message and exception type
- Processing duration
- Backoff strategy used
- Delay before this attempt

### Accessing Attempt History

```csharp
var jobStatus = await _jobStatusStore.GetStatusAsync(jobId);
if (jobStatus != null) {
    foreach (var attempt in jobStatus.Attempts) {
        _logger.LogInformation(
            "Attempt {AttemptNumber}: {Status} after {Duration}ms - {Error}",
            attempt.AttemptNumber,
            attempt.Succeeded ? "Success" : "Failed",
            attempt.DurationMs,
            attempt.ErrorMessage);
    }
}
```

### Dead-Letter Queue

Jobs that exhaust all retry attempts move to the dead-letter queue for manual intervention:

```csharp
// Get all dead-letter jobs
var deadLetterJobs = await _jobStatusStore.GetJobsByStatusAsync(
    BackgroundJobStatus.DeadLetter, 
    skip: 0,
    take: 100);

foreach (var job in deadLetterJobs) {
    _logger.LogError(
        "Dead-letter job {JobId}: {JobType} failed after {RetryCount} retries. Last error: {Error}",
        job.JobId,
        job.JobType,
        job.RetryCount,
        job.ErrorMessage);
    
    // Review attempt history
    foreach (var attempt in job.Attempts) {
        _logger.LogInformation(
            "  Attempt {Number}: {Error}",
            attempt.AttemptNumber,
            attempt.ErrorMessage);
    }
}
```

### Dead-Letter Queue Recovery

To retry a dead-letter job after fixing the underlying issue:

1. Retrieve the job from status store
2. Create a new job with the same payload
3. Optionally adjust MaxRetries or other parameters
4. Dispatch the new job

```csharp
var deadLetterJob = await _jobStatusStore.GetStatusAsync(jobId);
if (deadLetterJob != null && deadLetterJob.Status == BackgroundJobStatus.DeadLetter) {
    // Create new job with same payload but reset retry count
    var retryJob = new BackgroundJob {
        Type = deadLetterJob.JobType,
        Payload = /* original payload from job */,
        MaxRetries = 5,  // Maybe increase retries
        Priority = 10,   // Higher priority for manual retry
        Metadata = deadLetterJob.Metadata
    };
    
    await _jobDispatcher.DispatchAsync(retryJob);
}
```

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

## Built-in Job Handlers

### GeneratePlan Job Handler

The `GeneratePlanJobHandler` handles webhook-triggered plan generation asynchronously. When a GitHub issue is labeled with `copilot-assisted`, a webhook enqueues a `GeneratePlan` job instead of blocking the webhook response.

**Workflow:**
1. Webhook validates event and enqueues `GeneratePlanJob`
2. Webhook returns 202 Accepted with job ID and status URL
3. `GeneratePlanJobHandler` executes in background:
   - Creates working branch
   - Creates WIP pull request
   - Analyzes repository (optional, continues on failure)
   - Loads custom instructions (optional, continues on failure)
   - Generates plan using LLM
   - Updates task and PR with plan
   - Dispatches `ExecutePlanJob` for code execution

**Job Status Tracking:**
- Updates `IJobStatusStore` through lifecycle: Queued → Processing → Completed/Failed/Cancelled
- Stores result data (PR number, branch name) on success
- Stores error message on failure

**Payload:**
```csharp
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
```

**Example Dispatch:**
```csharp
var payload = new GeneratePlanJobPayload {
    TaskId = "owner/repo/issues/1",
    InstallationId = 123,
    RepositoryOwner = "owner",
    RepositoryName = "repo",
    IssueNumber = 1,
    IssueTitle = "Add new feature",
    IssueBody = "Description of feature",
    WebhookId = Guid.NewGuid().ToString()
};

var job = new BackgroundJob {
    Type = "GeneratePlan",
    Payload = JsonSerializer.Serialize(payload),
    Priority = 5, // Higher priority for user-triggered events
    Metadata = new Dictionary<string, string> {
        ["TaskId"] = payload.TaskId,
        ["WebhookId"] = payload.WebhookId
    }
};

var dispatched = await _jobDispatcher.DispatchAsync(job);
if (dispatched) {
    // Return 202 Accepted with job.Id for status tracking
    return Results.Accepted($"/jobs/{job.Id}/status", new { jobId = job.Id });
}
```

**Status Endpoint:**
```csharp
app.MapGet("/jobs/{jobId}/status", async (string jobId, IJobStatusStore statusStore) => {
    var status = await statusStore.GetStatusAsync(jobId);
    if (status == null) {
        return Results.NotFound();
    }
    return Results.Ok(status);
});
```

### ExecutePlan Job Handler

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

## Job Status Tracking and Monitoring

The RG.OpenCopilot background job system includes comprehensive status tracking and monitoring capabilities to help you understand job execution, identify bottlenecks, and troubleshoot failures.

### Job Lifecycle Statuses

Jobs transition through the following statuses during their lifecycle:

- **Queued**: Job is in the queue waiting to be processed
- **Processing**: Job is currently being executed
- **Completed**: Job finished successfully
- **Failed**: Job failed and will not be retried (or is not configured for retry)
- **Cancelled**: Job was cancelled during execution
- **Retried**: Job failed but will be retried
- **DeadLetter**: Job exceeded maximum retry attempts and moved to dead-letter queue

### Status Information

Each job's status includes detailed tracking information:

```csharp
public sealed class BackgroundJobStatusInfo {
    public string JobId { get; init; }              // Unique job identifier
    public string JobType { get; init; }            // Type of job (e.g., "GeneratePlan")
    public BackgroundJobStatus Status { get; init; } // Current status
    
    // Timestamps
    public DateTime CreatedAt { get; init; }        // When job was created
    public DateTime? StartedAt { get; init; }       // When processing started
    public DateTime? CompletedAt { get; init; }     // When job completed
    
    // Retry information
    public int RetryCount { get; init; }            // Current retry attempt
    public int MaxRetries { get; init; }            // Maximum retry attempts
    public DateTime? LastRetryAt { get; init; }     // When last retry occurred
    
    // Correlation tracking
    public string Source { get; init; }             // Source that triggered job
    public string? ParentJobId { get; init; }       // Parent job if spawned
    public string? CorrelationId { get; init; }     // Correlation ID for tracking
    
    // Performance metrics
    public long? ProcessingDurationMs { get; init; } // Processing duration
    public long? QueueWaitTimeMs { get; init; }      // Time spent in queue
    
    // Error handling
    public string? ErrorMessage { get; init; }       // Error message if failed
    public Dictionary<string, string> Metadata { get; init; } // Additional metadata
    public string? ResultData { get; init; }         // Job result data (JSON)
}
```

### Monitoring API Endpoints

The system exposes REST API endpoints for monitoring job status:

#### Get Job Status

Get status of a specific job:

```http
GET /jobs/{jobId}/status
```

Response:
```json
{
  "jobId": "abc123",
  "jobType": "GeneratePlan",
  "status": "Completed",
  "createdAt": "2025-12-08T12:00:00Z",
  "startedAt": "2025-12-08T12:00:05Z",
  "completedAt": "2025-12-08T12:00:30Z",
  "processingDurationMs": 25000,
  "queueWaitTimeMs": 5000,
  "source": "Webhook",
  "metadata": {
    "TaskId": "owner/repo/issues/1",
    "WebhookId": "webhook-123"
  }
}
```

#### List Jobs with Filtering

List jobs with optional filters:

```http
GET /jobs?status=failed&type=GeneratePlan&source=Webhook&skip=0&take=20
```

Query Parameters:
- `status`: Filter by job status (Queued, Processing, Completed, Failed, Cancelled, Retried, DeadLetter)
- `type`: Filter by job type (e.g., "GeneratePlan", "ExecutePlan")
- `source`: Filter by job source (e.g., "Webhook", "Manual")
- `skip`: Number of jobs to skip for pagination (default: 0)
- `take`: Number of jobs to return (default: 100, max: 100)

Response:
```json
{
  "jobs": [
    {
      "jobId": "abc123",
      "jobType": "GeneratePlan",
      "status": "Failed",
      ...
    }
  ],
  "count": 5,
  "skip": 0,
  "take": 20
}
```

#### Get Job Metrics

Get aggregated metrics for all jobs:

```http
GET /jobs/metrics
```

Response:
```json
{
  "totalJobs": 100,
  "queueDepth": 5,
  "processingCount": 2,
  "completedCount": 80,
  "failedCount": 10,
  "cancelledCount": 1,
  "deadLetterCount": 2,
  "averageProcessingDurationMs": 15000.0,
  "averageQueueWaitTimeMs": 2500.0,
  "failureRate": 0.10,
  "metricsByType": {
    "GeneratePlan": {
      "jobType": "GeneratePlan",
      "totalCount": 50,
      "successCount": 45,
      "failureCount": 5,
      "averageProcessingDurationMs": 12000.0,
      "failureRate": 0.10
    },
    "ExecutePlan": {
      "jobType": "ExecutePlan",
      "totalCount": 50,
      "successCount": 35,
      "failureCount": 5,
      "averageProcessingDurationMs": 18000.0,
      "failureRate": 0.10
    }
  }
}
```

#### Get Dead-Letter Queue

Get jobs in the dead-letter queue (jobs that exceeded max retries):

```http
GET /jobs/dead-letter?skip=0&take=20
```

Response:
```json
{
  "jobs": [
    {
      "jobId": "abc123",
      "jobType": "GeneratePlan",
      "status": "DeadLetter",
      "retryCount": 3,
      "maxRetries": 3,
      "errorMessage": "Failed to generate plan after 3 retries",
      ...
    }
  ],
  "count": 2,
  "skip": 0,
  "take": 20
}
```

### Programmatic Access

Access job status programmatically using `IJobStatusStore`:

```csharp
public class MyService {
    private readonly IJobStatusStore _statusStore;

    public MyService(IJobStatusStore statusStore) {
        _statusStore = statusStore;
    }

    public async Task<BackgroundJobStatusInfo?> GetJobStatusAsync(string jobId) {
        return await _statusStore.GetStatusAsync(jobId);
    }

    public async Task<List<BackgroundJobStatusInfo>> GetFailedJobsAsync() {
        return await _statusStore.GetJobsByStatusAsync(BackgroundJobStatus.Failed);
    }

    public async Task<JobMetrics> GetMetricsAsync() {
        return await _statusStore.GetMetricsAsync();
    }
}
```

### Monitoring Best Practices

1. **Track Dead-Letter Queue**: Monitor `/jobs/dead-letter` endpoint regularly to identify jobs that are consistently failing
2. **Set Up Alerts**:
   - Alert when `deadLetterCount` exceeds a threshold
   - Alert when `failureRate` exceeds a threshold (e.g., >20%)
   - Alert when `averageQueueWaitTimeMs` is too high (indicates queue backlog)
   - Alert when `queueDepth` exceeds capacity
3. **Monitor Performance Metrics**:
   - Track `averageProcessingDurationMs` to identify slow jobs
   - Track `averageQueueWaitTimeMs` to identify queue bottlenecks
   - Use `metricsByType` to identify problematic job types
4. **Use Correlation IDs**: Set correlation IDs on related jobs for end-to-end tracking
5. **Set Source Metadata**: Always set the `Source` metadata to track job origins

### Example Monitoring Dashboard Query

Example query to check system health:

```csharp
var metrics = await statusStore.GetMetricsAsync();

// Check for high failure rate
if (metrics.FailureRate > 0.2) {
    logger.LogWarning("High failure rate detected: {FailureRate:P}", metrics.FailureRate);
}

// Check for queue backlog
if (metrics.QueueDepth > 50) {
    logger.LogWarning("Queue backlog detected: {QueueDepth} jobs queued", metrics.QueueDepth);
}

// Check for dead-letter queue growth
if (metrics.DeadLetterCount > 10) {
    logger.LogError("Dead-letter queue growing: {DeadLetterCount} jobs failed permanently", 
        metrics.DeadLetterCount);
}

// Check for slow processing
if (metrics.AverageProcessingDurationMs > 30000) {
    logger.LogWarning("Average processing time is high: {Duration}ms", 
        metrics.AverageProcessingDurationMs);
}
```

### State Transition Logging

The job processor automatically logs all state transitions:

```
Information: Job abc123 of type GeneratePlan transitioned to Queued
Information: Job abc123 of type GeneratePlan transitioned to Processing (waited 2500ms in queue)
Information: Job abc123 completed successfully (processing took 15000ms)

Warning: Job def456 failed: Connection timeout
Information: Job def456 transitioned to Retried (attempt 1/3)

Error: Job ghi789 moved to DeadLetter queue after 3 retries
```

These logs can be integrated with centralized logging systems (e.g., ELK, Application Insights) for monitoring and alerting.

## Operational Playbooks

### Playbook: High Job Failure Rate

**Symptoms:**
- Failure rate > 20%
- Many jobs in dead-letter queue
- Repeated retry attempts

**Diagnosis Steps:**
1. Check metrics: `GET /jobs/metrics`
2. Identify failing job types: Review `metricsByType` in metrics response
3. Get recent failures: `GET /jobs?status=failed&take=50`
4. Review attempt history for patterns in error messages

**Common Causes and Fixes:**
- **External service down**: Temporarily increase retry delays, check service status
- **Invalid payload**: Fix payload generation logic, clear dead-letter queue
- **Resource exhaustion**: Increase `MaxConcurrency` or reduce job load
- **Transient network issues**: Ensure exponential backoff is enabled with jitter

**Actions:**
```bash
# Check current metrics
curl http://localhost:5000/jobs/metrics

# Get failed jobs with details
curl "http://localhost:5000/jobs?status=failed&type=GeneratePlan&take=20"

# Review dead-letter jobs
curl "http://localhost:5000/jobs/dead-letter?take=20"
```

### Playbook: Queue Backlog

**Symptoms:**
- `queueDepth` > 50
- High `averageQueueWaitTimeMs`
- Jobs waiting long before processing

**Diagnosis Steps:**
1. Check current queue depth: `GET /jobs/metrics`
2. Identify job type distribution in queue
3. Check if processors are stuck or slow

**Common Causes and Fixes:**
- **Insufficient concurrency**: Increase `MaxConcurrency` in config
- **Slow job handlers**: Optimize handler code or add timeout limits
- **Burst of jobs**: Increase `MaxQueueSize` temporarily
- **Stuck jobs**: Restart application to clear processors

**Actions:**
```bash
# Check queue status
curl http://localhost:5000/jobs/metrics | jq '.queueDepth'

# List queued jobs
curl "http://localhost:5000/jobs?status=queued&take=100"

# Restart app to clear stuck processors (if needed)
# systemctl restart opencopilot  # or appropriate restart command
```

### Playbook: Dead-Letter Queue Growth

**Symptoms:**
- `deadLetterCount` increasing
- Jobs exhausting all retries
- Persistent failures

**Diagnosis Steps:**
1. Get all dead-letter jobs: `GET /jobs/dead-letter`
2. Review attempt history for each job
3. Identify common error patterns
4. Check if issue is fixed

**Recovery Steps:**
```csharp
// 1. Get dead-letter jobs
var deadLetterJobs = await statusStore.GetJobsByStatusAsync(
    BackgroundJobStatus.DeadLetter);

// 2. Review errors and fix root cause
foreach (var job in deadLetterJobs) {
    // Check attempt history
    foreach (var attempt in job.Attempts) {
        Console.WriteLine($"Attempt {attempt.AttemptNumber}: {attempt.ErrorMessage}");
    }
}

// 3. After fixing issue, retry dead-letter jobs
foreach (var deadJob in deadLetterJobs) {
    var retryJob = new BackgroundJob {
        Type = deadJob.JobType,
        Payload = /* get from original job */,
        MaxRetries = 5,
        Priority = 10  // High priority for recovery
    };
    await jobDispatcher.DispatchAsync(retryJob);
}

// 4. Clean up old dead-letter jobs after successful retry
foreach (var deadJob in deadLetterJobs) {
    await statusStore.DeleteStatusAsync(deadJob.JobId);
}
```

### Playbook: Duplicate Job Detection

**Symptoms:**
- Jobs with same idempotency key rejected
- Dispatch returns `false`
- Warnings in logs about duplicate jobs

**Diagnosis Steps:**
1. Check if duplicate is intentional or bug
2. Review idempotency key generation logic
3. Check if previous job is stuck

**Common Causes and Fixes:**
- **Retry logic bug**: Fix caller to not retry on success
- **Stuck in-flight job**: Clear deduplication cache or restart
- **Race condition**: Add proper synchronization in caller

**Actions:**
```csharp
// Check if job with key is in-flight
var inFlightJobId = await deduplicationService.GetInFlightJobAsync(idempotencyKey);
if (inFlightJobId != null) {
    // Check job status
    var status = await statusStore.GetStatusAsync(inFlightJobId);
    if (status != null) {
        Console.WriteLine($"Job {inFlightJobId} is {status.Status}");
        
        // If job is stuck (processing > 30 minutes), may need to cancel
        if (status.Status == BackgroundJobStatus.Processing && 
            DateTime.UtcNow - status.StartedAt > TimeSpan.FromMinutes(30)) {
            // Cancel stuck job
            jobDispatcher.CancelJob(inFlightJobId);
        }
    }
}
```

### Playbook: Slow Job Processing

**Symptoms:**
- `averageProcessingDurationMs` > 30 seconds
- Jobs timing out
- High CPU/memory usage

**Diagnosis Steps:**
1. Identify slow job types from metrics
2. Review job handler code for inefficiencies
3. Check for external service latency
4. Monitor resource utilization

**Optimization Steps:**
- Add caching for frequently accessed data
- Implement pagination for large data processing
- Add timeouts to external service calls
- Consider breaking large jobs into smaller chunks
- Use async/await properly to avoid blocking

**Monitoring:**
```csharp
// Get per-type metrics
var metrics = await statusStore.GetMetricsAsync();
foreach (var typeMetric in metrics.MetricsByType.Values) {
    if (typeMetric.AverageProcessingDurationMs > 30000) {
        Console.WriteLine(
            $"Slow job type: {typeMetric.JobType} - " +
            $"Avg duration: {typeMetric.AverageProcessingDurationMs}ms");
    }
}
```

### Best Practices for Operations

1. **Monitor Continuously**
   - Set up alerts for high failure rates (> 20%)
   - Alert on queue depth (> 50 jobs)
   - Alert on dead-letter growth (> 10 jobs)
   - Monitor average processing time

2. **Regular Maintenance**
   - Review dead-letter queue weekly
   - Clean up old completed jobs monthly
   - Analyze metrics trends for capacity planning
   - Update retry policies based on observed patterns

3. **Incident Response**
   - Keep logs for at least 30 days
   - Document failure patterns and resolutions
   - Test recovery procedures regularly
   - Have runbooks for common issues

4. **Capacity Planning**
   - Monitor queue depth trends
   - Track peak vs. average load
   - Plan for 2x capacity during peak times
   - Consider auto-scaling based on queue depth

5. **Testing**
   - Test retry logic with simulated failures
   - Verify idempotency with duplicate requests
   - Load test with realistic job volumes
   - Test dead-letter queue recovery procedures
