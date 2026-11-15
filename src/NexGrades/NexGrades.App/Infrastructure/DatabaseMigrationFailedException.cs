namespace NexGrades.App.Infrastructure;

/// <summary>
/// A migration of an existing database failed after a backup of it had been taken. <see cref="StartupFailureHandler"/>
/// tells the user where that backup is. The message is constant and names no path; the path is only in
/// <see cref="BackupFilePath"/>, and the cause is the inner exception.
/// </summary>
/// <param name="backupFilePath">The full path of the backup taken before the migration.</param>
/// <param name="innerException">Why the migration failed.</param>
public sealed class DatabaseMigrationFailedException(string backupFilePath, Exception innerException)
    : Exception(DefaultMessage, innerException)
{
    /// <summary>The constant diagnostic message. It carries no path, so it is safe in any log.</summary>
    public const string DefaultMessage = "The database migration failed after a backup was taken.";

    /// <summary>The full path of the backup taken before the migration. It carries the Windows user name.</summary>
    public string BackupFilePath { get; } = backupFilePath;
}
