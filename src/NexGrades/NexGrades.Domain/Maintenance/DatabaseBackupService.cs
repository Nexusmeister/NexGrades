using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Maintenance;

/// <summary>
/// Copies the database to a backup file before an update changes its schema, so a failed or unwanted migration never
/// costs the teacher's data. The copy uses SQLite's online backup, is checked with <c>PRAGMA quick_check</c>, and only
/// then gets its final name, so a backup file that exists is complete. The newest <see cref="RetainedBackupCount"/>
/// backups are kept.
/// </summary>
/// <param name="contextFactory">Creates the context whose connection is the source of the copy.</param>
/// <param name="clock">Source of "now" for the timestamp in the file name.</param>
/// <param name="logger">Receives fixed messages and counts; never a path or database content.</param>
public sealed partial class DatabaseBackupService(
    IDbContextFactory<AppDbContext> contextFactory,
    IClock clock,
    ILogger<DatabaseBackupService> logger)
{
    /// <summary>How many backups <see cref="PruneBackups"/> keeps.</summary>
    public const int RetainedBackupCount = 5;

    private const string TemporaryExtension = ".tmp";

    /// <summary>
    /// Copies the database into <paramref name="backupDirectory"/>, creating the folder if needed, and then prunes the
    /// folder to <see cref="RetainedBackupCount"/> backups. A temporary copy left by an earlier failed attempt is
    /// removed first. An existing backup is never overwritten: a name that is taken gets a suffix (<c>-1</c>,
    /// <c>-2</c>, ...).
    /// </summary>
    /// <param name="backupDirectory">The folder for the backups.</param>
    /// <param name="previousAppVersion">The app version that last ran against the database, for the file name; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The full path of the new backup file.</returns>
    /// <exception cref="InvariantViolationException">The database is not a SQLite database, or the copy failed its integrity check.</exception>
    /// <exception cref="IOException">The folder or the file could not be written.</exception>
    public Task<string> CreateBackupAsync(string backupDirectory, string? previousAppVersion, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("DatabaseBackupService.CreateBackup", async () =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

            _ = Directory.CreateDirectory(backupDirectory);
            DeleteLeftoverTemporaryFiles(backupDirectory);

            var fileName = DatabaseBackupFileName.Create(clock.Now, previousAppVersion);
            var temporaryPath = Path.Combine(backupDirectory, fileName + TemporaryExtension);

            await CopyDatabaseAsync(temporaryPath, cancellationToken);
            var backupPath = MoveToFreeName(temporaryPath, backupDirectory, fileName);
            LogBackupCreated();

            _ = PruneBackups(backupDirectory);
            return backupPath;
        });

    /// <summary>
    /// Deletes all but the newest <see cref="RetainedBackupCount"/> backups in <paramref name="backupDirectory"/>.
    /// Only files named by <see cref="DatabaseBackupFileName"/> are considered, newest first by the timestamp in the
    /// name, then by file name. Pruning is housekeeping: a file or folder that cannot be read or deleted is logged as
    /// a warning and skipped, never thrown.
    /// </summary>
    /// <param name="backupDirectory">The backup folder.</param>
    /// <returns>How many backups were deleted.</returns>
    public int PruneBackups(string backupDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectory);

        List<(string Path, string Name, DateTime Timestamp)> backups;
        try
        {
            if (!Directory.Exists(backupDirectory))
            {
                return 0;
            }

            backups = [];
            foreach (var path in Directory.EnumerateFiles(backupDirectory, DatabaseBackupFileName.Prefix + "*" + DatabaseBackupFileName.Extension))
            {
                var name = Path.GetFileName(path);
                if (DatabaseBackupFileName.TryParse(name, out var timestamp))
                {
                    backups.Add((path, name, timestamp));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPruneFailed(ex.GetType().Name);
            return 0;
        }

        var deleted = 0;
        foreach (var backup in backups
            .OrderByDescending(b => b.Timestamp)
            .ThenByDescending(b => b.Name, StringComparer.Ordinal)
            .Skip(RetainedBackupCount))
        {
            try
            {
                File.Delete(backup.Path);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogPruneFailed(ex.GetType().Name);
            }
        }

        if (deleted > 0)
        {
            LogBackupsPruned(deleted);
        }

        return deleted;
    }

    private async Task CopyDatabaseAsync(string temporaryPath, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (context.Database.GetDbConnection() is not SqliteConnection source)
        {
            throw new InvariantViolationException("A database backup needs a SQLite database.");
        }

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // No pooling, so disposing the connection really closes the file and it can be renamed.
            var destinationString = new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString();

            await using var destination = new SqliteConnection(destinationString);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);

            await using var check = destination.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            var result = await check.ExecuteScalarAsync(cancellationToken) as string;
            if (!string.Equals(result, "ok", StringComparison.Ordinal))
            {
                throw new InvariantViolationException("The database backup failed its integrity check.");
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static string MoveToFreeName(string temporaryPath, string backupDirectory, string fileName)
    {
        var stem = fileName[..^DatabaseBackupFileName.Extension.Length];
        for (var attempt = 0; ; attempt++)
        {
            var candidate = attempt == 0 ? fileName : $"{stem}-{attempt}{DatabaseBackupFileName.Extension}";
            var path = Path.Combine(backupDirectory, candidate);
            if (File.Exists(path))
            {
                continue;
            }

            try
            {
                File.Move(temporaryPath, path, overwrite: false);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
                // Taken between the check and the move; try the next suffix.
            }
        }
    }

    /// <summary>
    /// Removes the temporary copies an interrupted backup left behind. Only this service's own files are touched, and
    /// a file that cannot be deleted is skipped: the new copy has its own name.
    /// </summary>
    private void DeleteLeftoverTemporaryFiles(string backupDirectory)
    {
        var pattern = DatabaseBackupFileName.Prefix + "*" + DatabaseBackupFileName.Extension + TemporaryExtension;
        foreach (var path in Directory.EnumerateFiles(backupDirectory, pattern))
        {
            var name = Path.GetFileName(path);
            if (!name.EndsWith(TemporaryExtension, StringComparison.Ordinal)
                || !DatabaseBackupFileName.TryParse(name[..^TemporaryExtension.Length], out _))
            {
                continue;
            }

            try
            {
                File.Delete(path);
                LogLeftoverDeleted();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogLeftoverDeleteFailed(ex.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database backup created")]
    private partial void LogBackupCreated();

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} old database backups")]
    private partial void LogBackupsPruned(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted an unfinished database backup from an earlier attempt")]
    private partial void LogLeftoverDeleted();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Deleting an unfinished database backup from an earlier attempt failed: {ExceptionType}")]
    private partial void LogLeftoverDeleteFailed(string exceptionType);

    // Only the type is logged: the exception message names the path, which carries the Windows user name.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Pruning old database backups failed: {ExceptionType}")]
    private partial void LogPruneFailed(string exceptionType);
}
