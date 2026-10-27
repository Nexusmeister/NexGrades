namespace NexGrades.Common.Storage;

/// <summary>
/// The per-user folder where NexGrades keeps everything that must survive app updates (database, and later
/// logs). It lives outside the install folder on purpose: the updater replaces the binaries.
/// </summary>
public interface IAppDataPaths
{
    /// <summary>Root folder for all per-user application data.</summary>
    string DataDirectory { get; }

    /// <summary>Full path of the default SQLite database file.</summary>
    string DatabaseFilePath { get; }

    /// <summary>Creates <see cref="DataDirectory"/> if it does not exist yet. Safe to call repeatedly.</summary>
    void EnsureCreated();
}
