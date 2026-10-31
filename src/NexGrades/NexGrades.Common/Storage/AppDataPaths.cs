namespace NexGrades.Common.Storage;

/// <summary>
/// Default <see cref="IAppDataPaths"/>. The root is a constructor argument so tests can use a temp folder.
/// </summary>
public sealed class AppDataPaths(string dataDirectory) : IAppDataPaths
{
    /// <summary>File name of the database inside the data directory (unchanged from the legacy name).</summary>
    public const string DatabaseFileName = "app.db";

    public string DataDirectory { get; } = Path.GetFullPath(dataDirectory);

    public string DatabaseFilePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>Uses <c>%LOCALAPPDATA%\NexGrades</c>.</summary>
    public static AppDataPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexGrades"));

    public void EnsureCreated() => Directory.CreateDirectory(DataDirectory);
}
