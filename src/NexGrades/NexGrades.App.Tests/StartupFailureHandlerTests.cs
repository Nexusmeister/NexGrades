using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;

namespace NexGrades.App.Tests;

public sealed class StartupFailureHandlerTests
{
    private const string SecretText = "secret-pupil-path-Zebra42";

    private static readonly InvalidOperationException _failure = new(SecretText);

    [Fact]
    public void Handle_LogsCriticalWithException_WhenPrimaryLoggerPresent()
    {
        var harness = new Harness();
        var primary = new CapturingLogger<object>();
        var fallback = new CapturingLogger<object>();

        harness.Handler.Handle(_failure, () => primary, () => fallback, "logs");

        var entry = Assert.Single(primary.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Same(_failure, entry.Exception);
        Assert.Empty(fallback.Entries);
    }

    [Fact]
    public void Handle_UsesFallbackLogger_WhenPrimaryReturnsNull()
    {
        var harness = new Harness();
        var fallback = new CapturingLogger<object>();

        harness.Handler.Handle(_failure, () => null, () => fallback, "logs");

        var entry = Assert.Single(fallback.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Same(_failure, entry.Exception);
    }

    [Fact]
    public void Handle_UsesFallbackLogger_WhenPrimaryResolverThrows()
    {
        var harness = new Harness();
        var fallback = new CapturingLogger<object>();

        harness.Handler.Handle(_failure, () => throw new InvalidOperationException("no host"), () => fallback, "logs");

        Assert.Single(fallback.Entries);
        Assert.Equal([StartupFailureHandler.FailureExitCode], harness.ExitCodes);
    }

    [Fact]
    public void Handle_ShowsDialogAndExits_WhenNoLoggerAvailable()
    {
        var harness = new Harness();

        var ex = Record.Exception(() => harness.Handler.Handle(_failure, () => null, () => null, "logs"));

        Assert.Null(ex);
        Assert.Single(harness.Dialog.Messages);
        Assert.Equal([1], harness.ExitCodes);
    }

    [Fact]
    public void Handle_ShowsDialogExactlyOnce_WhenLoggerPresent()
    {
        var harness = new Harness();

        harness.Handler.Handle(_failure, () => new CapturingLogger<object>(), () => null, "logs");

        Assert.Single(harness.Dialog.Messages);
    }

    [Fact]
    public void Handle_ExitsWithOne_OnNormalFailure()
    {
        var harness = new Harness();

        harness.Handler.Handle(_failure, () => new CapturingLogger<object>(), () => null, "logs");

        Assert.Equal([1], harness.ExitCodes);
    }

    [Fact]
    public void Handle_ExitsWithOne_WhenDialogThrows()
    {
        var harness = new Harness { Dialog = { Throw = true } };

        var ex = Record.Exception(() => harness.Handler.Handle(_failure, () => null, () => null, "logs"));

        Assert.Null(ex);
        Assert.Equal([1], harness.ExitCodes);
    }

    [Fact]
    public void Handle_ShowsDialogAndExitsWithOne_WhenLoggerThrows()
    {
        var harness = new Harness();

        var ex = Record.Exception(() => harness.Handler.Handle(_failure, () => new ThrowingLogger(), () => null, "logs"));

        Assert.Null(ex);
        Assert.Single(harness.Dialog.Messages);
        Assert.Equal([1], harness.ExitCodes);
    }

    [Fact]
    public void Handle_ShowsDirectoryWithoutExceptionText_WhenLogDirectoryGiven()
    {
        var harness = new Harness();

        harness.Handler.Handle(_failure, () => new CapturingLogger<object>(), () => null, "/var/logs/nexgrades");

        var message = Assert.Single(harness.Dialog.Messages);
        Assert.Contains("/var/logs/nexgrades", message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretText, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Handle_ShowsMessageWithoutLogFolder_WhenNoLogDirectory(string? logDirectory)
    {
        var harness = new Harness();

        harness.Handler.Handle(_failure, () => null, () => null, logDirectory);

        var message = Assert.Single(harness.Dialog.Messages);
        Assert.Equal(StartupFailureHandler.MessageWithoutLogFolder, message);
        Assert.DoesNotContain(SecretText, message, StringComparison.Ordinal);
    }

    private sealed class Harness
    {
        public Harness() => Handler = new StartupFailureHandler(Dialog, ExitCodes.Add);

        public FakeDialog Dialog { get; } = new();

        public List<int> ExitCodes { get; } = [];

        public StartupFailureHandler Handler { get; }
    }

    private sealed class FakeDialog : IStartupErrorDialog
    {
        public List<string> Messages { get; } = [];

        public bool Throw { get; set; }

        public void Show(string message)
        {
            Messages.Add(message);
            if (Throw)
            {
                throw new InvalidOperationException("dialog failed");
            }
        }
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("logger failed");
    }
}
