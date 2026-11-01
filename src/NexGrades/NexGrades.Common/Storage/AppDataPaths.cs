namespace NexGrades.Common.Storage;

/// <summary>
/// Default <see cref="IAppDataPaths"/>. The root is a constructor argument so tests can use a temp folder.
/// </summary>
/// <param name="dataDirectory">The root folder for per-user data. It is made absolute when the instance is created.</param>
public sealed class AppDataPaths(string dataDirectory) : IAppDataPaths
{
    /// <summary>File name of the database inside the data directory (unchanged from the legacy name).</summary>
    public const string DatabaseFileName = "app.db";

    /// <summary>Name of the log folder inside the data directory.</summary>
    public const string LogDirectoryName = "logs";

    /// <summary>Absolute path of the data folder. It is resolved once, so later changes to the working directory do not move it.</summary>
    public string DataDirectory { get; } = Path.GetFullPath(dataDirectory);

    /// <summary>Absolute path of the database file inside <see cref="DataDirectory"/>.</summary>
    public string DatabaseFilePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>Absolute path of the log folder inside <see cref="DataDirectory"/>.</summary>
    public string LogDirectory => Path.Combine(DataDirectory, LogDirectoryName);

    /// <summary>Uses <c>%LOCALAPPDATA%\NexGrades</c>.</summary>
    public static AppDataPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexGrades"));

    /// <summary>Creates <see cref="DataDirectory"/> if it does not exist yet.</summary>
    public void EnsureCreated() => Directory.CreateDirectory(DataDirectory);
}
