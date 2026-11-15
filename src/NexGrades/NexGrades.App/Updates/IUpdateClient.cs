using Velopack;

namespace NexGrades.App.Updates;

/// <summary>
/// The installer's update API, reduced to what <see cref="UpdateCoordinator"/> needs, so the coordinator can be tested
/// without an installed app or a network. Nothing may do I/O or network work until a member is called.
/// </summary>
public interface IUpdateClient
{
    /// <summary>
    /// True when the app runs from an installation that can update itself. It is false in development, under Aspire and
    /// in tests; then no other member may be called.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>The installed version, or <see langword="null"/> when <see cref="IsInstalled"/> is false.</summary>
    SemanticVersion? CurrentVersion { get; }

    /// <summary>
    /// An update downloaded earlier that is newer than the installed version and has not been applied yet, or
    /// <see langword="null"/>. Reads the local package folder.
    /// </summary>
    UpdateCandidate? PendingRestart { get; }

    /// <summary>
    /// Asks <paramref name="channel"/>'s feed for an update newer than the installed version. A channel that has no feed
    /// (HTTP 404) offers nothing.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <param name="cancellationToken">Abandons the check, for example on a timeout.</param>
    /// <returns>The newest update of the channel, or <see langword="null"/> when there is none.</returns>
    Task<UpdateCandidate?> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken);

    /// <summary>Downloads <paramref name="candidate"/> into the local package folder so it can be applied on exit.</summary>
    /// <param name="candidate">An update returned by <see cref="CheckAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    Task DownloadAsync(UpdateCandidate candidate, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the installer's updater, which waits for this process to exit, installs <paramref name="candidate"/> without
    /// a window and, if asked, starts the app again. It returns at once; the caller then lets the app exit normally.
    /// </summary>
    /// <param name="candidate">A downloaded update.</param>
    /// <param name="restart">Whether the app starts again after the update.</param>
    void ApplyOnExit(UpdateCandidate candidate, bool restart);
}
