using Microsoft.Extensions.Logging;

namespace NexGrades.App.Tests;

/// <summary>Records every call so tests can assert on level, message text and exception separately.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
