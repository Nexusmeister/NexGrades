using Microsoft.Extensions.Logging;
using NexGrades.App.Logging;
using NexGrades.Domain.Exceptions;

namespace NexGrades.App.Tests;

public class UnhandledExceptionLoggerTests
{
    private const string Secret = "ZZ-SECRET";

    private sealed class Counter
    {
        public int Count { get; private set; }

        public void Increment() => Count++;
    }

    private static (UnhandledExceptionLogger Sut, CapturingLogger<UnhandledExceptionLogger> Logger, Counter Flushes) Create()
    {
        var logger = new CapturingLogger<UnhandledExceptionLogger>();
        var flushes = new Counter();
        return (new UnhandledExceptionLogger(logger, flushes.Increment), logger, flushes);
    }

    [Fact]
    public void OnDomainException_LogsCriticalAndFlushes_WhenTerminating()
    {
        var (sut, logger, flushes) = Create();
        var exception = new InvariantViolationException(Secret);

        sut.OnDomainException(exception, isTerminating: true);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("terminating: True", entry.Message, StringComparison.Ordinal);
        Assert.Equal(1, flushes.Count);
    }

    [Fact]
    public void OnDomainException_LogsCriticalWithoutFlushing_WhenNotTerminating()
    {
        var (sut, logger, flushes) = Create();

        sut.OnDomainException(new InvalidOperationException("x"), isTerminating: false);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Contains("terminating: False", entry.Message, StringComparison.Ordinal);
        Assert.Equal(0, flushes.Count);
    }

    [Fact]
    public void OnDomainException_LogsCriticalAndFlushes_ForNonExceptionObject()
    {
        var (sut, logger, flushes) = Create();

        sut.OnDomainException(Secret, isTerminating: true);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
        Assert.Equal(1, flushes.Count);
    }

    [Fact]
    public void OnDomainException_Logs_ForNullObject()
    {
        var (sut, logger, _) = Create();

        sut.OnDomainException(null, isTerminating: false);

        Assert.Equal(LogLevel.Critical, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public void OnDispatcherException_LogsErrorWithExceptionAttached_WithoutFlushing()
    {
        var (sut, logger, flushes) = Create();
        var exception = new InvalidOperationException(Secret);

        sut.OnDispatcherException(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Equal(0, flushes.Count);
    }

    [Fact]
    public void OnUnobservedTaskException_LogsErrorWithAggregateAttached()
    {
        var (sut, logger, flushes) = Create();
        var aggregate = new AggregateException(new InvariantViolationException(Secret));

        sut.OnUnobservedTaskException(aggregate);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(aggregate, entry.Exception);
        Assert.Equal(0, flushes.Count);
    }

    [Fact]
    public void MessageText_NeverContainsTheExceptionMessage_ForAnyHandler()
    {
        var (sut, logger, _) = Create();
        var exception = new InvariantViolationException(Secret);

        sut.OnDispatcherException(exception);
        sut.OnDomainException(exception, isTerminating: true);
        sut.OnUnobservedTaskException(new AggregateException(exception));

        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.DoesNotContain(Secret, e.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void OnDomainException_LogsExceptionOnce_WhenSameInstanceFollowsDispatcherAndTerminates()
    {
        var (sut, logger, flushes) = Create();
        var exception = new InvalidOperationException(Secret);

        sut.OnDispatcherException(exception);
        sut.OnDomainException(exception, isTerminating: true);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Single(logger.Entries, e => ReferenceEquals(e.Exception, exception));
        var second = logger.Entries[1];
        Assert.Equal(LogLevel.Critical, second.Level);
        Assert.Null(second.Exception);
        Assert.Contains("terminating: True", second.Message, StringComparison.Ordinal);
        Assert.Equal(1, flushes.Count);
    }

    [Fact]
    public void OnDomainException_DoesNotLogExceptionAgainOrFlush_WhenSameInstanceIsNotTerminating()
    {
        var (sut, logger, flushes) = Create();
        var exception = new InvalidOperationException(Secret);

        sut.OnDispatcherException(exception);
        sut.OnDomainException(exception, isTerminating: false);

        Assert.Single(logger.Entries, e => ReferenceEquals(e.Exception, exception));
        Assert.Equal(0, flushes.Count);
    }

    [Fact]
    public void OnDomainException_LogsDifferentExceptionInFull_AfterDispatcherException()
    {
        var (sut, logger, flushes) = Create();
        var other = new FormatException("other");

        sut.OnDispatcherException(new InvalidOperationException(Secret));
        sut.OnDomainException(other, isTerminating: true);

        var entry = logger.Entries[1];
        Assert.Same(other, entry.Exception);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Equal(1, flushes.Count);
    }

    [Fact]
    public void OnDomainException_LogsInFull_WhenNoDispatcherExceptionCameBefore()
    {
        var (sut, logger, _) = Create();
        var exception = new InvariantViolationException(Secret);

        sut.OnDomainException(exception, isTerminating: false);

        Assert.Same(exception, Assert.Single(logger.Entries).Exception);
    }
}
