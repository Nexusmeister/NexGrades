using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Logging;
using NexGrades.App.Logging;
using NexGrades.Data.Services;
using NexGrades.Domain.Maintenance;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// The startup step that brings the database schema up to date, taking a backup first whenever an existing database is
/// about to be migrated (typically the first start after an update). If the backup fails, nothing is migrated and the
/// start fails with <see cref="DatabaseBackupFailedException"/>; if the migration fails after a backup, the start fails
/// with <see cref="DatabaseMigrationFailedException"/>, which names the backup. After a successful start against the
/// default database, the running version is remembered so the next backup can be named after it.
/// </summary>
/// <param name="migrationService">Applies the pending migrations and calls the backup step before them.</param>
/// <param name="backupService">Takes the backup.</param>
/// <param name="backupLocation">Where backups go; no backup is taken when it has no folder.</param>
/// <param name="databaseLocation">Whether the database is the default one, whose version is remembered.</param>
/// <param name="settingsStore">Holds the version that last ran.</param>
/// <param name="loggerFactory">Creates the logger, and the local-only logger for lines that name the backup path.</param>
public sealed partial class StartupDatabaseMigrator(
    DatabaseMigrationService migrationService,
    DatabaseBackupService backupService,
    DatabaseBackupLocation backupLocation,
    DatabaseLocation databaseLocation,
    IUserSettingsStore settingsStore,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<StartupDatabaseMigrator>();
    private readonly ILogger _localLogger = loggerFactory.CreateLogger(StartupDiagnostics.LocalOnlyCategory);

    /// <summary>Backs up the database if it is about to be migrated, migrates it and remembers the running version.</summary>
    /// <param name="cancellationToken">Cancels the backup and the migration.</param>
    /// <exception cref="DatabaseBackupFailedException">The backup failed; the database is unchanged.</exception>
    /// <exception cref="DatabaseMigrationFailedException">The migration failed after a backup was taken.</exception>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        string? backupPath = null;
        try
        {
            await migrationService.RunAsync(
                async token => backupPath = await BackUpAsync(token),
                cancellationToken);
        }
        catch (Exception ex) when (backupPath is not null && ex is not DatabaseBackupFailedException)
        {
            LogMigrationFailedAfterBackup(_localLogger, backupPath);
            throw new DatabaseMigrationFailedException(backupPath, ex);
        }

        if (!databaseLocation.IsOverride)
        {
            await RememberRunningVersionAsync(cancellationToken);
        }
    }

    /// <summary>Takes the backup; returns its path, or <see langword="null"/> when this database has no backup folder.</summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever stops the backup must stop the migration and end in the backup-failed dialog.")]
    private async Task<string?> BackUpAsync(CancellationToken cancellationToken)
    {
        if (backupLocation.Directory is not { } directory)
        {
            LogNoBackupFolder(_logger);
            return null;
        }

        try
        {
            var previousVersion = await ReadLastRunVersionAsync(cancellationToken);
            var path = await backupService.CreateBackupAsync(directory, previousVersion, cancellationToken);
            LogBackupLocation(_localLogger, path);
            return path;
        }
        catch (Exception ex)
        {
            // Logged once, with this exception, by the startup failure handler.
            throw new DatabaseBackupFailedException(ex);
        }
    }

    /// <summary>
    /// The version only names the backup file, so a settings file that cannot be read does not stop the backup: the
    /// backup is then named with an unknown version.
    /// </summary>
    private async Task<string?> ReadLastRunVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await settingsStore.LoadAsync(cancellationToken)).LastRunVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSettingsUnreadable(_logger, ex.GetType().Name);
            return null;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Remembering the version is bookkeeping for the next backup's name; the app starts regardless.")]
    private async Task RememberRunningVersionAsync(CancellationToken cancellationToken)
    {
        var version = AppVersion.Current;
        try
        {
            var settings = await settingsStore.LoadAsync(cancellationToken);
            if (settings.LastRunVersion != version)
            {
                _ = await settingsStore.UpdateAsync(s => s with { LastRunVersion = version }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            LogRememberVersionFailed(_logger, ex.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database is not a file; migrating without a backup")]
    private static partial void LogNoBackupFolder(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database backed up before the migration to {BackupPath}")]
    private static partial void LogBackupLocation(ILogger logger, string backupPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "The database migration failed; the backup taken before it is {BackupPath}")]
    private static partial void LogMigrationFailedAfterBackup(ILogger logger, string backupPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The settings file could not be read; the backup is named without a version: {ExceptionType}")]
    private static partial void LogSettingsUnreadable(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The running version could not be saved to the settings file: {ExceptionType}")]
    private static partial void LogRememberVersionFailed(ILogger logger, string exceptionType);
}
