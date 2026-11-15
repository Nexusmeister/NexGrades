using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace NexGrades.App.Updates;

/// <summary>
/// <see cref="IUpdateClient"/> on Velopack. One <see cref="UpdateManager"/> per channel is created on first use, never
/// in the constructor, and only when the app is installed: Velopack's locator exists only after
/// <c>VelopackApp.Build().Run()</c> in <c>Program.Main</c>, and in tests not at all.
/// </summary>
/// <remarks>
/// <para>
/// The stable channel reads <c>&lt;Repository&gt;/releases/latest/download/</c>. GitHub redirects that to the release
/// marked "latest", which the release workflow sets for stable releases only, so the check needs no GitHub API call
/// (no rate limit) and is not limited to the first page of releases. The beta channel uses Velopack's
/// <see cref="GithubSource"/> with pre-releases, which makes one unauthenticated API call for the ten newest releases,
/// and only when the user opted in to betas.
/// </para>
/// <para>
/// Every check passes the channel explicitly and forbids downgrades, so switching betas off never installs an older
/// stable version. Velopack's own logger is not bridged into the app log: its messages contain local paths and URLs.
/// </para>
/// </remarks>
/// <param name="options">Where the release feeds are.</param>
public sealed class VelopackUpdateClient(IOptions<UpdateFeedOptions> options) : IUpdateClient
{
    private readonly Lock _gate = new();
    private readonly Dictionary<UpdateChannel, UpdateManager> _managers = [];

    /// <inheritdoc />
    public bool IsInstalled => InstalledLocator() is not null;

    /// <inheritdoc />
    public SemanticVersion? CurrentVersion => InstalledLocator()?.CurrentlyInstalledVersion;

    /// <inheritdoc />
    public UpdateCandidate? PendingRestart
    {
        get
        {
            if (InstalledLocator() is not { } locator)
            {
                return null;
            }

            var channel = ChannelOf(locator.Channel);
            return GetManager(channel).UpdatePendingRestart is { } asset
                ? new UpdateCandidate(asset.Version, channel, IsDowngrade: false) { Handle = asset }
                : null;
        }
    }

    /// <inheritdoc />
    public async Task<UpdateCandidate?> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        var manager = GetManager(channel);

        // Velopack takes no token here. A check abandoned on timeout finishes in the background; its failure is observed
        // so it does not resurface as an unobserved task exception.
        var check = manager.CheckForUpdatesAsync();
        _ = check.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        UpdateInfo? info;
        try
        {
            info = await check.WaitAsync(cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // No feed file for this channel yet (for example no stable release so far).
            return null;
        }

        return info is null
            ? null
            : new UpdateCandidate(info.TargetFullRelease.Version, channel, info.IsDowngrade) { Handle = new PreparedUpdate(manager, info) };
    }

    /// <inheritdoc />
    public Task DownloadAsync(UpdateCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Handle switch
        {
            PreparedUpdate prepared => prepared.Manager.DownloadUpdatesAsync(prepared.Info, progress: null, cancellationToken),
            // A pending update is already on disk.
            VelopackAsset => Task.CompletedTask,
            _ => throw new ArgumentException("The update was not created by this client.", nameof(candidate)),
        };
    }

    /// <inheritdoc />
    public void ApplyOnExit(UpdateCandidate candidate, bool restart)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var asset = candidate.Handle switch
        {
            PreparedUpdate prepared => prepared.Info.TargetFullRelease,
            VelopackAsset pending => pending,
            _ => throw new ArgumentException("The update was not created by this client.", nameof(candidate)),
        };

        // Not ApplyUpdatesAndRestart: that ends the process at once, before the host stops and the log is flushed.
        GetManager(candidate.Channel).WaitExitThenApplyUpdates(asset, silent: true, restart: restart);
    }

    /// <summary>The channel name the release workflow packs with (<c>vpk pack --channel</c>).</summary>
    internal static string ChannelName(UpdateChannel channel) => channel switch
    {
        UpdateChannel.Beta => "beta",
        _ => "stable",
    };

    /// <summary>The source of <paramref name="channel"/>'s feed; see the class remarks.</summary>
    internal static IUpdateSource CreateSource(UpdateFeedOptions feed, UpdateChannel channel)
    {
        ArgumentNullException.ThrowIfNull(feed);

        if (!string.IsNullOrWhiteSpace(feed.SourceOverride))
        {
            return Uri.TryCreate(feed.SourceOverride, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? new SimpleWebSource(uri)
                : new SimpleFileSource(new DirectoryInfo(feed.SourceOverride));
        }

        if (string.IsNullOrWhiteSpace(feed.Repository))
        {
            throw new InvalidOperationException("Updates:Repository is not configured.");
        }

        var repository = feed.Repository.TrimEnd('/');
        return channel == UpdateChannel.Beta
            ? new GithubSource(repository, accessToken: null, prerelease: true)
            : new SimpleWebSource(repository + "/releases/latest/download/");
    }

    private static UpdateChannel ChannelOf(string? channelName) =>
        string.Equals(channelName, ChannelName(UpdateChannel.Beta), StringComparison.OrdinalIgnoreCase) ? UpdateChannel.Beta : UpdateChannel.Stable;

    /// <summary>Velopack's locator when the app is installed; <see langword="null"/> otherwise, without touching disk or network.</summary>
    private static IVelopackLocator? InstalledLocator()
    {
        try
        {
            return VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null
                ? VelopackLocator.Current
                : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private UpdateManager GetManager(UpdateChannel channel)
    {
        if (InstalledLocator() is null)
        {
            throw new InvalidOperationException("Updates are only available in an installed app.");
        }

        lock (_gate)
        {
            if (!_managers.TryGetValue(channel, out var manager))
            {
                var updateOptions = new UpdateOptions
                {
                    ExplicitChannel = ChannelName(channel),
                    AllowVersionDowngrade = false,
                };
                manager = new UpdateManager(CreateSource(options.Value, channel), updateOptions);
                _managers[channel] = manager;
            }

            return manager;
        }
    }

    /// <summary>What <see cref="CheckAsync"/> found, kept so the download goes through the same source.</summary>
    private sealed record PreparedUpdate(UpdateManager Manager, UpdateInfo Info);
}
