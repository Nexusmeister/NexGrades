namespace NexGrades.App.Infrastructure;

/// <summary>
/// The backup that must precede a migration of an existing database could not be taken, so the migration did not
/// run and the database is unchanged. <see cref="StartupFailureHandler"/> words this case for the user. The message
/// is constant; the cause is the inner exception.
/// </summary>
/// <param name="innerException">Why the backup failed.</param>
public sealed class DatabaseBackupFailedException(Exception innerException) : Exception(DefaultMessage, innerException)
{
    /// <summary>The constant diagnostic message. It carries no path, so it is safe in any log.</summary>
    public const string DefaultMessage = "The database could not be backed up before the migration; the migration did not run.";
}
