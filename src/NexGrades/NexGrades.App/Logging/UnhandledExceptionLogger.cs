using Microsoft.Extensions.Logging;

namespace NexGrades.App.Logging;

/// <summary>
/// Records exceptions that nothing else caught (UI thread, any thread, abandoned tasks). The handlers only log;
/// they never mark an exception as handled, so the app's behaviour on a crash is unchanged.
/// </summary>
/// <param name="logger">Where the exceptions are written.</param>
/// <param name="flush">Writes buffered log output to disk; called before a terminating crash.</param>
public sealed partial class UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger, Action flush)
{
    /// <summary>Logs an exception thrown on the UI thread that no handler caught.</summary>
    /// <param name="exception">The unhandled exception.</param>
    public void OnDispatcherException(Exception exception) => LogDispatcherException(exception);

    /// <summary>
    /// Logs an exception from any thread (<c>AppDomain.UnhandledException</c>). When the runtime is about to
    /// terminate, the log is flushed so the entry survives.
    /// </summary>
    /// <param name="exceptionObject">The thrown object; normally an <see cref="Exception"/>.</param>
    /// <param name="isTerminating">Whether the runtime is going to terminate.</param>
    public void OnDomainException(object? exceptionObject, bool isTerminating)
    {
        if (exceptionObject is Exception exception)
        {
            LogDomainException(exception, isTerminating);
        }
        else
        {
            LogNonExceptionObject(isTerminating);
        }

        if (isTerminating)
        {
            flush();
        }
    }

    /// <summary>
    /// Logs a faulted task whose exception nobody observed. The caller marks it observed so it cannot take the
    /// process down later.
    /// </summary>
    /// <param name="exception">The aggregate that carries the task's exceptions.</param>
    public void OnUnobservedTaskException(AggregateException exception) => LogUnobservedTaskException(exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on the UI thread")]
    private partial void LogDispatcherException(Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception; terminating: {IsTerminating}")]
    private partial void LogDomainException(Exception exception, bool isTerminating);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled non-exception object thrown; terminating: {IsTerminating}")]
    private partial void LogNonExceptionObject(bool isTerminating);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception")]
    private partial void LogUnobservedTaskException(Exception exception);
}
