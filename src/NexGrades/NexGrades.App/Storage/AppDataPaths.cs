using System.IO;

namespace NexGrades.App.Storage;

/// <summary>
/// Default <see cref="IAppDataPaths"/>. The root is a constructor argument so tests can use a temp folder.
/// </summary>
/// <param name="dataDirectory">The root folder for per-user data. It is made absolute when the instance is created.</param>
/// <param name="settingsFilePathOverride">
/// A settings file to use instead of <c>settings.json</c> in the data folder, or <see langword="null"/> or blank for the
/// default. It lets a development or test run keep its settings apart from the real ones.
/// </param>
public sealed class AppDataPaths(string dataDirectory, string? settingsFilePathOverride = null) : IAppDataPaths
{
    /// <summary>Configuration key that overrides <see cref="SettingsFilePath"/>.</summary>
    public const string SettingsFileConfigurationKey = "Settings:FilePath";

    /// <summary>File name of the database inside the data directory (unchanged from the legacy name).</summary>
    public const string DatabaseFileName = "app.db";

    /// <summary>Name of the log folder inside the data directory.</summary>
    public const string LogDirectoryName = "logs";

    /// <summary>File name of the user settings file inside the data directory.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>Name of the database backup folder inside the data directory.</summary>
    public const string BackupDirectoryName = "backups";

    /// <summary>Absolute path of the data folder. It is resolved once, so later changes to the working directory do not move it.</summary>
    public string DataDirectory { get; } = Path.GetFullPath(dataDirectory);

    /// <summary>Absolute path of the database file inside <see cref="DataDirectory"/>.</summary>
    public string DatabaseFilePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>Absolute path of the log folder inside <see cref="DataDirectory"/>.</summary>
    public string LogDirectory => Path.Combine(DataDirectory, LogDirectoryName);

    /// <summary>Absolute path of the user settings file: the override when one was given, else inside <see cref="DataDirectory"/>.</summary>
    public string SettingsFilePath { get; } = string.IsNullOrWhiteSpace(settingsFilePathOverride)
        ? Path.Combine(Path.GetFullPath(dataDirectory), SettingsFileName)
        : Path.GetFullPath(settingsFilePathOverride);

    /// <summary>Absolute path of the database backup folder inside <see cref="DataDirectory"/>.</summary>
    public string BackupDirectory => Path.Combine(DataDirectory, BackupDirectoryName);

    /// <summary>Uses <c>%LOCALAPPDATA%\NexGrades</c>.</summary>
    /// <param name="settingsFilePathOverride">An alternative settings file, or <see langword="null"/> for the default.</param>
    public static AppDataPaths CreateDefault(string? settingsFilePathOverride = null) =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexGrades"), settingsFilePathOverride);

    /// <summary>Creates <see cref="DataDirectory"/> if it does not exist yet.</summary>
    public void EnsureCreated() => Directory.CreateDirectory(DataDirectory);
}
