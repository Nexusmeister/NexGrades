using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.App.Infrastructure;
using NexGrades.Common.Storage;

namespace NexGrades.App.Logging;

/// <summary>
/// The first and last lines of every log: what is running where, so a log file can be read without asking the
/// user for details. Only environment facts are logged: no names, no ids, and never the full connection string.
/// </summary>
public static partial class StartupDiagnostics
{
    /// <summary>Logs version, operating system, .NET runtime, environment, database file and log folder.</summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="location">Where the database lives.</param>
    /// <param name="paths">The per-user folders.</param>
    public static void LogStartup(ILogger logger, IHostEnvironment environment, DatabaseLocation location, IAppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(paths);

        var version = AppVersion();
        var operatingSystem = RuntimeInformation.OSDescription;
        var framework = RuntimeInformation.FrameworkDescription;
        var environmentName = environment.EnvironmentName;
        var databasePath = DatabasePath(location);
        var logDirectory = paths.LogDirectory;

        LogStarting(logger, version, operatingSystem, framework, environmentName);
        LogLocations(logger, databasePath, logDirectory);
    }

    /// <summary>Logs that the app has begun to shut down.</summary>
    /// <param name="logger">The logger to write to.</param>
    public static void LogShuttingDown(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        LogShuttingDownMessage(logger);
    }

    /// <summary>Logs the clean end of the process.</summary>
    /// <param name="logger">The logger to write to.</param>
    public static void LogShutdown(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        LogStopped(logger);
    }

    private static string AppVersion() =>
        typeof(StartupDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>
    /// The default database is a plain path. A configured connection string may carry other settings, so only its
    /// data source is taken from it.
    /// </summary>
    private static string DatabasePath(DatabaseLocation location)
    {
        try
        {
            return new SqliteConnectionStringBuilder(location.ConnectionString).DataSource;
        }
        catch (ArgumentException)
        {
            return "unknown";
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "NexGrades {Version} starting on {OperatingSystem}, {Framework}, environment {Environment}")]
    private static partial void LogStarting(ILogger logger, string version, string operatingSystem, string framework, string environment);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database file {DatabasePath}; log folder {LogDirectory}")]
    private static partial void LogLocations(ILogger logger, string databasePath, string logDirectory);

    [LoggerMessage(Level = LogLevel.Information, Message = "NexGrades is shutting down")]
    private static partial void LogShuttingDownMessage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "NexGrades stopped")]
    private static partial void LogStopped(ILogger logger);
}
