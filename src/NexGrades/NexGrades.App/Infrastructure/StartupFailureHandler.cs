using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NexGrades.App.Localization;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// The single place a failed start ends: it logs the failure, shows one error dialog and exits with a non-zero
/// code. It works at every stage of startup, including before the host exists, so no failure is swallowed or
/// escapes as an unhandled exception without a message to the user.
/// </summary>
/// <param name="dialog">Shows the error to the user.</param>
/// <param name="exit">Ends the application with the given exit code.</param>
public sealed partial class StartupFailureHandler(IStartupErrorDialog dialog, Action<int> exit)
{
    /// <summary>The exit code of a process whose start failed.</summary>
    public const int FailureExitCode = 1;

    /// <summary>
    /// Logs <paramref name="exception"/>, shows the error dialog once and exits with <see cref="FailureExitCode"/>.
    /// Nothing in here lets an exception escape: a resolver or logger that fails is skipped, and the exit action
    /// runs even if the dialog cannot be shown.
    /// </summary>
    /// <param name="exception">The startup failure. Only the logger receives it; the dialog never shows its text.</param>
    /// <param name="primaryLogger">
    /// Resolves the host's logger; may return <see langword="null"/> or throw when the host is not built yet.
    /// </param>
    /// <param name="fallbackLogger">
    /// Resolves the crash logger that exists as soon as the file logger does, used when the primary one is not
    /// available; may return <see langword="null"/>.
    /// </param>
    /// <param name="logDirectory">
    /// Resolves the log folder to mention in the dialog. It returns <see langword="null"/> when no file log exists,
    /// and a throwing resolver is treated the same way, so the dialog then omits the folder.
    /// </param>
    /// <param name="markFailed">
    /// Optional telemetry bookkeeping for the failure. It runs guarded: whatever it throws is ignored.
    /// </param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The failure path must always reach the dialog and the exit, whatever the logger, the resolvers or the dialog throw.")]
    public void Handle(
        Exception exception,
        Func<ILogger?> primaryLogger,
        Func<ILogger?> fallbackLogger,
        Func<string?> logDirectory,
        Action<Exception>? markFailed = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(primaryLogger);
        ArgumentNullException.ThrowIfNull(fallbackLogger);
        ArgumentNullException.ThrowIfNull(logDirectory);

        try
        {
            markFailed?.Invoke(exception);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Recording the startup failure in telemetry failed: {ex.GetType().Name}");
        }

        try
        {
            var logger = Resolve(primaryLogger) ?? Resolve(fallbackLogger);
            if (logger is not null)
            {
                LogStartupFailed(logger, exception);
            }
            else
            {
                // No logger can exist yet (for example a malformed settings file). Type only: the message may name paths.
                Debug.WriteLine($"NexGrades could not start: {exception.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logging the startup failure failed: {ex.GetType().Name}");
        }

        try
        {
            dialog.Show(BuildMessage(exception, ResolveDirectory(logDirectory)));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Showing the startup error failed: {ex.GetType().Name}");
        }
        finally
        {
            exit(FailureExitCode);
        }
    }

    /// <summary>
    /// The dialog text for <paramref name="exception"/>: a failed pre-migration backup and a failed migration after a
    /// backup get their own text, every other failure the generic one. The log folder is mentioned when it is known.
    /// Exception text is never part of it. The text comes straight from <see cref="TextResources"/>, which needs no
    /// host, so it works when host creation itself failed.
    /// </summary>
    /// <param name="exception">The startup failure; only its type and, after a backup, the backup path are used.</param>
    /// <param name="logDirectory">The log folder, or <see langword="null"/> when no file log exists.</param>
    /// <returns>The text to show.</returns>
    public static string BuildMessage(Exception exception, string? logDirectory)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var hasLogFolder = !string.IsNullOrWhiteSpace(logDirectory);
        return exception switch
        {
            DatabaseBackupFailedException => hasLogFolder
                ? TextResources.Format("Infrastructure_Startup_BackupFailed_WithLogFolder_Format", logDirectory)
                : TextResources.Text("Infrastructure_Startup_BackupFailed"),
            DatabaseMigrationFailedException migrationFailed => hasLogFolder
                ? TextResources.Format("Infrastructure_Startup_MigrationFailed_WithLogFolder_Format", migrationFailed.BackupFilePath, logDirectory)
                : TextResources.Format("Infrastructure_Startup_MigrationFailed_Format", migrationFailed.BackupFilePath),
            _ => hasLogFolder
                ? TextResources.Format("Infrastructure_Startup_Failed_WithLogFolder_Format", logDirectory)
                : TextResources.Text("Infrastructure_Startup_Failed"),
        };
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A folder that cannot be resolved is the same as no known folder; the dialog then omits it.")]
    private static string? ResolveDirectory(Func<string?> resolver)
    {
        try
        {
            return resolver();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A logger that cannot be resolved is the same as no logger; the next one is tried.")]
    private static ILogger? Resolve(Func<ILogger?> resolver)
    {
        try
        {
            return resolver();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "NexGrades could not start")]
    private static partial void LogStartupFailed(ILogger logger, Exception exception);
}
