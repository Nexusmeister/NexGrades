namespace NexGrades.Common.Storage;

/// <summary>
/// The per-user folder where NexGrades keeps everything that must survive app updates (database and
/// log files). It lives outside the install folder on purpose: the updater replaces the binaries.
/// </summary>
public interface IAppDataPaths
{
    /// <summary>Root folder for all per-user application data.</summary>
    string DataDirectory { get; }

    /// <summary>Full path of the default SQLite database file.</summary>
    string DatabaseFilePath { get; }

    /// <summary>
    /// Folder for the rolling log files. It is not created by <see cref="EnsureCreated"/>: logging creates it
    /// itself and tolerates failure, so an unwritable folder never blocks startup.
    /// </summary>
    string LogDirectory { get; }

    /// <summary>
    /// Full path of the user settings file (for example the update preferences). It sits next to the database so
    /// it survives app updates, and is created on the first save, not by <see cref="EnsureCreated"/>.
    /// </summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Default folder for the database backups taken before a migration. It is created when the first backup is
    /// written, not by <see cref="EnsureCreated"/>.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>Creates <see cref="DataDirectory"/> if it does not exist yet. Safe to call repeatedly.</summary>
    void EnsureCreated();
}
