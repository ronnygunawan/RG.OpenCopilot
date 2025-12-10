using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Test implementation of ILogger that captures log messages for assertions
/// </summary>
internal sealed class TestLogger<T> : ILogger<T> {
    public record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
    
    public List<LogEntry> LogEntries { get; } = [];
    public List<string> LoggedMessages => LogEntries.Select(e => e.Message).ToList();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        var message = formatter(state, exception);
        LogEntries.Add(new LogEntry(logLevel, message, exception));
    }
}
