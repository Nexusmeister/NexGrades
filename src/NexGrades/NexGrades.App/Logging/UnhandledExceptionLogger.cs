using Microsoft.Extensions.Logging;

namespace NexGrades.App.Logging;

/// <summary>
/// Records exceptions that nothing else caught (UI thread, any thread, abandoned tasks). The handlers only log;
/// they never mark an exception as handled, so the app's behaviour on a crash is unchanged. An exception that
/// reaches the dispatcher handler and then terminates the process through the domain handler is written once in
/// full; the second report is a short line without the exception.
/// </summary>
/// <param name="logger">Where the exceptions are written.</param>
/// <param name="flush">Writes buffered log output to disk; called before a terminating crash.</param>
public sealed partial class UnhandledExceptionLogger(ILogger<UnhandledExceptionLogger> logger, Action flush)
{
    // Remembers the last UI-thread exception so the same instance is not logged in full a second time when it
    // later arrives as the terminating exception. Weak, so the logger never keeps an exception alive. This is
    // best effort: WPF may wrap the exception on its way to the domain handler, in which case the duplicate
    // stays. Nothing is dropped silently either way, because a match still logs a line.
    private WeakReference<Exception>? _lastDispatcherException;

    /// <summary>Logs an exception thrown on the UI thread that no handler caught.</summary>
    /// <param name="exception">The unhandled exception.</param>
    public void OnDispatcherException(Exception exception)
    {
        _lastDispatcherException = new WeakReference<Exception>(exception);
        LogDispatcherException(exception);
    }

    /// <summary>
    /// Logs an exception from any thread (<c>AppDomain.UnhandledException</c>). When the runtime is about to
    /// terminate, the log is flushed so the entry survives. An exception already logged by
    /// <see cref="OnDispatcherException"/> is not written again, only noted.
    /// </summary>
    /// <param name="exceptionObject">The thrown object; normally an <see cref="Exception"/>.</param>
    /// <param name="isTerminating">Whether the runtime is going to terminate.</param>
    public void OnDomainException(object? exceptionObject, bool isTerminating)
    {
        if (exceptionObject is Exception exception)
        {
            var alreadyLogged = _lastDispatcherException is { } last
                && last.TryGetTarget(out var previous)
                && ReferenceEquals(previous, exception);
            if (alreadyLogged)
            {
                _lastDispatcherException = null;
                LogAlreadyLoggedException(isTerminating);
            }
            else
            {
                LogDomainException(exception, isTerminating);
            }
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

    [LoggerMessage(Level = LogLevel.Critical, Message = "Previously logged unhandled UI exception; terminating: {IsTerminating}")]
    private partial void LogAlreadyLoggedException(bool isTerminating);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled non-exception object thrown; terminating: {IsTerminating}")]
    private partial void LogNonExceptionObject(bool isTerminating);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unobserved task exception")]
    private partial void LogUnobservedTaskException(Exception exception);
}
