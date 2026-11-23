using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NexGrades.App.Storage;

namespace NexGrades.App.Infrastructure;

/// <summary>
/// Where the database backups taken before a migration go. <paramref name="Directory"/> is <see langword="null"/>
/// when the database is not a file (for example an in-memory database), so there is nothing to back up.
/// </summary>
/// <param name="Directory">The absolute backup folder, or <see langword="null"/> for no backups.</param>
public sealed record DatabaseBackupLocation(string? Directory)
{
    /// <summary>The configuration key that overrides the backup folder.</summary>
    public const string ConfigurationKey = "Backup:Directory";

    /// <summary>
    /// Picks the backup folder: the configured <see cref="ConfigurationKey"/> first; for a database whose location is
    /// an override, a <c>backups</c> folder next to that database file, so a development or test database never writes
    /// into the teacher's real backup folder; otherwise <see cref="IAppDataPaths.BackupDirectory"/>. Nothing is
    /// created here.
    /// </summary>
    /// <param name="configuration">The app configuration.</param>
    /// <param name="location">Where the database lives.</param>
    /// <param name="paths">The per-user folders.</param>
    /// <returns>The backup location; its folder is <see langword="null"/> when the database is not a file.</returns>
    public static DatabaseBackupLocation Resolve(IConfiguration configuration, DatabaseLocation location, IAppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(paths);

        var configured = configuration[ConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new DatabaseBackupLocation(Path.GetFullPath(configured));
        }

        if (!location.IsOverride)
        {
            return new DatabaseBackupLocation(paths.BackupDirectory);
        }

        var databaseFile = DatabaseFile(location.ConnectionString);
        var databaseFolder = databaseFile is null ? null : Path.GetDirectoryName(databaseFile);
        return new DatabaseBackupLocation(databaseFolder is null ? null : Path.Combine(databaseFolder, AppDataPaths.BackupDirectoryName));
    }

    /// <summary>The full path of the database file, or <see langword="null"/> for an in-memory or URI data source.</summary>
    private static string? DatabaseFile(string connectionString)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            var dataSource = builder.DataSource;
            if (builder.Mode == SqliteOpenMode.Memory
                || string.IsNullOrWhiteSpace(dataSource)
                || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
                || dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.GetFullPath(dataSource);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
