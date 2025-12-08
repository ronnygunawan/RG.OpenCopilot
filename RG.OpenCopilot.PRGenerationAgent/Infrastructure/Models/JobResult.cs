namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Represents the result of a job execution
/// </summary>
public sealed class JobResult {
    /// <summary>
    /// Whether the job completed successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception details if the job failed
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Whether the job should be retried
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    /// Optional result data from the job
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static JobResult CreateSuccess(object? data = null) => new() {
        Success = true,
        Data = data
    };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static JobResult CreateFailure(string errorMessage, Exception? exception = null, bool shouldRetry = false) => new() {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception,
        ShouldRetry = shouldRetry
    };
}
