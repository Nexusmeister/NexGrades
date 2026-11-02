using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;
using NexGrades.Common.Storage;
using Serilog;
using Serilog.Core;

namespace NexGrades.App.Logging;

/// <summary>
/// Creates the rolling file logger: one file per day (<c>nexgrades-20261031.log</c>) that also rolls when it
/// reaches the size limit, with a bounded number of files kept. Logging is a diagnostic aid, so every failure to
/// set it up is tolerated and never blocks startup.
/// </summary>
public static class FileLogging
{
    /// <summary>File name pattern; Serilog inserts the date before the extension.</summary>
    public const string FileNamePattern = "nexgrades-.log";

    private const long BytesPerMegabyte = 1_048_576;

    /// <summary>
    /// Creates the log folder and the file logger, or returns <see langword="null"/> when the folder cannot be used
    /// (read-only profile, full disk, folder blocked by a file).
    /// </summary>
    /// <param name="paths">Provides the log folder.</param>
    /// <param name="options">Retention and size limits.</param>
    /// <param name="onFailure">
    /// Called with the failure so tests can observe it. Without it only the exception type is written to the debug
    /// output, because the message may contain paths.
    /// </param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Logging must never prevent the app from starting, whatever the reason the folder or file cannot be used.")]
    public static Logger? TryCreate(IAppDataPaths paths, FileLoggingOptions options, Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return Build(paths, options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File logging is disabled: {ex.GetType().Name}");
            onFailure?.Invoke(ex);
            return null;
        }
    }

    /// <summary>
    /// Reads the options from the configuration, resolves the log folder and creates the file logger, or returns
    /// <see langword="null"/> when any of that fails. Everything that can throw is inside one guarded section.
    /// </summary>
    /// <param name="configuration">The app configuration the <see cref="FileLoggingOptions"/> are read from.</param>
    /// <param name="paths">Provides the log folder; the per-user data folder when <see langword="null"/>.</param>
    /// <param name="onFailure">Called with the failure so tests can observe it; see the other overload.</param>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Logging must never prevent the app from starting, whatever the reason the folder or file cannot be used.")]
    public static Logger? TryCreate(IConfiguration configuration, IAppDataPaths? paths, Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            var resolved = paths ?? AppDataPaths.CreateDefault();
            var options = FileLoggingOptions.FromConfiguration(configuration);
            return Build(resolved, options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"File logging is disabled: {ex.GetType().Name}");
            onFailure?.Invoke(ex);
            return null;
        }
    }

    private static Logger Build(IAppDataPaths paths, FileLoggingOptions options)
    {
        var normalized = options.Normalize();
        _ = Directory.CreateDirectory(paths.LogDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                new LogFileFormatter(),
                Path.Combine(paths.LogDirectory, FileNamePattern),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: normalized.MaxFileSizeMegabytes * BytesPerMegabyte,
                retainedFileCountLimit: normalized.RetainedFileCount,
                shared: true,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .CreateLogger();
    }

    /// <summary>
    /// Writes out and closes the file logger, for the moment before the process dies from an unhandled exception.
    /// Does nothing when file logging is not active.
    /// </summary>
    public static void Flush() => Log.CloseAndFlush();
}
