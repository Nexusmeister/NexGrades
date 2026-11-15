namespace NexGrades.App.Infrastructure;

/// <summary>
/// The user's app preferences that are kept outside the database, in <c>settings.json</c> next to it, so they can be
/// read before the database is migrated. A missing or unreadable file means these defaults.
/// </summary>
public sealed record UserSettings
{
    /// <summary>Whether the app checks for updates shortly after it starts. On by default.</summary>
    public bool AutoCheckForUpdates { get; init; } = true;

    /// <summary>Whether beta releases are offered as updates as well. Off by default, and turning it off never downgrades.</summary>
    public bool ReceiveBetaUpdates { get; init; }

    /// <summary>
    /// The app version that last started successfully against the default database, or <see langword="null"/> when
    /// unknown. It names the backup taken before the next migration.
    /// </summary>
    public string? LastRunVersion { get; init; }
}
