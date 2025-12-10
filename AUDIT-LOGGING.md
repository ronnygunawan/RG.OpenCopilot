# Audit Logging and Observability

This document describes the audit logging and observability features in RG.OpenCopilot.

## Overview

RG.OpenCopilot includes comprehensive audit logging and health monitoring capabilities to track all important operations, state transitions, and system health.

## Audit Logging

### Features

- **Structured Logging**: All audit events are logged in a structured format with consistent fields
- **Correlation IDs**: Every audit event includes a correlation ID to track related operations
- **Event Types**: Multiple event types for different operations (webhooks, state transitions, API calls, etc.)
- **Duration Tracking**: API calls and operations include duration measurements
- **Error Tracking**: Failed operations include error messages for debugging

### Audit Event Types

| Event Type | Description |
|------------|-------------|
| `WebhookReceived` | GitHub webhook event received |
| `WebhookValidation` | Webhook signature validation result |
| `TaskStateTransition` | Agent task status change |
| `GitHubApiCall` | GitHub API operation (create PR, branch, etc.) |
| `JobStateTransition` | Background job status change |
| `ContainerOperation` | Docker container operation |
| `FileOperation` | File system operation |
| `PlanGeneration` | Plan generation started or completed |
| `PlanExecution` | Plan execution started or completed |

### Usage Example

```csharp
// Inject IAuditLogger into your service
public class MyService {
    private readonly IAuditLogger _auditLogger;

    public MyService(IAuditLogger auditLogger) {
        _auditLogger = auditLogger;
    }

    public async Task ProcessWebhookAsync(string eventType) {
        var correlationId = Guid.NewGuid().ToString();
        
        // Log webhook received
        _auditLogger.LogWebhookReceived(
            eventType: eventType,
            correlationId: correlationId,
            data: new Dictionary<string, object> {
                ["repository"] = "owner/repo",
                ["action"] = "labeled"
            });
    }
}
```

### Correlation ID Propagation

Correlation IDs are automatically propagated through the system:

1. **Webhook Events**: Generated when webhook is received
2. **Background Jobs**: Included in job metadata
3. **Task Operations**: Tracked through task lifecycle
4. **API Calls**: Associated with originating webhook/job

This allows tracing a complete operation from webhook receipt through execution to completion.

## Health Check Endpoints

### Basic Health Check

**Endpoint**: `GET /health`

Returns a simple "ok" response for basic liveness checks.

```bash
curl http://localhost:5000/health
```

Response:
```
ok
```

### Detailed Health Check

**Endpoint**: `GET /health/detailed`

Returns comprehensive health information including component status and metrics.

```bash
curl http://localhost:5000/health/detailed
```

Response example:
```json
{
  "status": "Healthy",
  "timestamp": "2025-12-10T05:00:00Z",
  "components": {
    "database": {
      "status": "Healthy",
      "description": "Database connection successful",
      "details": {
        "storage_type": "postgresql",
        "database": "opencopilot"
      }
    },
    "job_queue": {
      "status": "Healthy",
      "description": "Job queue operational",
      "details": {
        "queue_depth": 5
      }
    },
    "job_processing": {
      "status": "Healthy",
      "description": "Background job processing system",
      "details": {
        "queue_depth": 5,
        "processing_count": 2,
        "completed_count": 150,
        "failed_count": 3,
        "failure_rate": 0.02
      }
    }
  },
  "metrics": {
    "total_jobs": 155,
    "queue_depth": 5,
    "processing_count": 2,
    "failure_rate": 0.02,
    "average_processing_duration_ms": 2500.5
  }
}
```

### Health Status Values

| Status | Description | HTTP Code |
|--------|-------------|-----------|
| `Healthy` | All systems operational | 200 |
| `Degraded` | System functional but with issues (e.g., high queue depth, elevated failure rate) | 200 |
| `Unhealthy` | System has critical issues | 503 |

### Health Thresholds

- **Job Queue Degraded**: Queue depth > 1000 jobs
- **Job Processing Degraded**: Failure rate > 20%
- **Job Processing Unhealthy**: Failure rate > 50%

## Monitoring and Observability

### Key Metrics

The system tracks:

- **Job Metrics**: Total jobs, success/failure counts, processing duration, queue wait time
- **Queue Depth**: Number of jobs waiting for processing
- **Failure Rate**: Percentage of failed jobs
- **Processing Duration**: Average time to process jobs
- **State Transitions**: All task and job status changes

### Structured Logging Format

All audit logs follow this format:

```
[AUDIT] {EventType}: {Description} | Data: {EventData}
```

Example:
```
[AUDIT] WebhookReceived: Webhook received: issues | Data: {"EventType":"WebhookReceived","Timestamp":"2025-12-10T05:00:00Z","Description":"Webhook received: issues","CorrelationId":"abc123","action":"labeled","issueNumber":42}
```

### Log Levels

- **Information**: Successful operations (webhooks, state transitions, API calls)
- **Warning**: Validation failures, skipped operations
- **Error**: Failed operations with error details

## Integration with Monitoring Systems

The structured audit logs and health check endpoints can be integrated with:

- **Application Insights**: Structured logs can be ingested for analysis
- **Prometheus**: Health check endpoint can be scraped for metrics
- **Grafana**: Create dashboards based on metrics from `/health/detailed`
- **ELK Stack**: Parse JSON audit logs for visualization
- **Datadog**: Use structured logging for APM and monitoring

## Best Practices

1. **Always use correlation IDs** when logging related operations
2. **Include meaningful context** in audit event data
3. **Monitor health check endpoint** for system health
4. **Set up alerts** for unhealthy or degraded states
5. **Review audit logs** for security and compliance
6. **Track failure rates** to identify systemic issues

## Security Considerations

- Audit logs may contain sensitive information (repository names, issue numbers)
- Ensure proper access controls on log storage
- Sanitize log input to prevent log injection attacks
- Correlation IDs do not contain sensitive information
- Health check endpoint can be exposed for monitoring but contains system metrics

## Future Enhancements

Planned improvements:

- Rate limit tracking for GitHub API calls
- Performance metrics per job type
- Historical trends in health check response
- Alerting configuration via appsettings.json
- Custom health check components
