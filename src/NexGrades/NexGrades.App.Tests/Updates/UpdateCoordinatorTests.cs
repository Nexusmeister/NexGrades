using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexGrades.App.Infrastructure;
using NexGrades.App.Tests.TestSupport;
using NexGrades.App.Updates;

namespace NexGrades.App.Tests.Updates;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await gates they own and release themselves.")]
public class UpdateCoordinatorTests
{
    private readonly FakeUpdateClient _client = new();
    private readonly FakeUserSettingsStore _store = new();
    private readonly CapturingLogger<UpdateCoordinator> _logger = new();

    private UpdateCoordinator Create(TimeSpan? checkTimeout = null) =>
        new(_client, _store, Options.Create(new UpdateFeedOptions { CheckTimeout = checkTimeout ?? TimeSpan.FromSeconds(30) }), _logger);

    [Fact]
    public async Task CheckNowAsync_ReturnsNotInstalledWithoutTouchingTheClient_WhenNotInstalled()
    {
        _client.Installed = false;
        var coordinator = Create();

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.NotInstalled, outcome);
        Assert.Equal(UpdateState.Inactive, coordinator.State);
        Assert.False(coordinator.IsAvailable);
        Assert.Empty(_client.Checks);
        Assert.Equal(0, _client.Downloads);
        Assert.Equal(0, _store.LoadCalls);
        Assert.Equal(1, _client.PropertyReads);
    }

    [Fact]
    public async Task CheckOnStartupAsync_DoesNothing_WhenNotInstalled()
    {
        _client.Installed = false;
        _client.Pending = FakeUpdateClient.Candidate("0.2.0");
        var coordinator = Create();

        await coordinator.CheckOnStartupAsync();

        Assert.Equal(UpdateState.Inactive, coordinator.State);
        Assert.Equal(0, _store.LoadCalls);
        Assert.Empty(_client.Checks);
        Assert.Equal(1, _client.PropertyReads);
    }

    [Fact]
    public void Constructor_IsInactive_WhenReadingTheInstallStateThrows()
    {
        _client.IsInstalledFailure = new InvalidOperationException("no locator");

        var coordinator = Create();

        Assert.Equal(UpdateState.Inactive, coordinator.State);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CheckOnStartupAsync_DoesNotCheck_WhenAutomaticChecksAreOff()
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false };
        var coordinator = Create();

        await coordinator.CheckOnStartupAsync();

        Assert.Empty(_client.Checks);
        Assert.Equal(UpdateState.Idle, coordinator.State);
    }

    [Fact]
    public async Task CheckNowAsync_Checks_WhenAutomaticChecksAreOff()
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false };

        var outcome = await Create().CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Equal([UpdateChannel.Stable], _client.Checks);
    }

    [Fact]
    public async Task CheckOnStartupAsync_Checks_WhenAutomaticChecksAreOn()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        var coordinator = Create();

        await coordinator.CheckOnStartupAsync();
        await coordinator.DownloadCompletion;

        Assert.Equal([UpdateChannel.Stable], _client.Checks);
        Assert.Equal(UpdateState.ReadyToInstall, coordinator.State);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsOfflineAndIdle_WhenEveryChannelFails()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };
        _ = _client.Fails(UpdateChannel.Stable).Fails(UpdateChannel.Beta);
        var coordinator = Create();

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.Offline, outcome);
        Assert.Equal(UpdateState.Idle, coordinator.State);
        Assert.Null(coordinator.ReadyVersion);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsOffline_WhenTheChannelTimesOut()
    {
        _client.Checkers[UpdateChannel.Stable] = async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        };
        var coordinator = Create(TimeSpan.FromMilliseconds(50));

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.Offline, outcome);
        Assert.Equal(UpdateState.Idle, coordinator.State);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsUpdateFound_WhenOneChannelFailsAndTheOtherFindsOne()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };
        _ = _client.Fails(UpdateChannel.Stable).Returns(UpdateChannel.Beta, FakeUpdateClient.Candidate("0.2.0-beta.1", UpdateChannel.Beta));
        var coordinator = Create();

        var outcome = await coordinator.CheckNowAsync();
        await coordinator.DownloadCompletion;

        Assert.Equal(UpdateCheckOutcome.UpdateFound, outcome);
        Assert.Equal("0.2.0-beta.1", coordinator.ReadyVersion);
        Assert.Equal(UpdateState.ReadyToInstall, coordinator.State);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsUpToDate_WhenOneChannelFailsAndTheOtherHasNothing()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };
        _ = _client.Fails(UpdateChannel.Stable);

        var outcome = await Create().CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsUpToDateAndIdle_WhenFeedsOfferNothing()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };
        _ = _client.Returns(UpdateChannel.Stable, null).Returns(UpdateChannel.Beta, null);
        var coordinator = Create();

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Equal(UpdateState.Idle, coordinator.State);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsUpToDate_WhenOfferedVersionIsNotNewer()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.1.0"));

        var outcome = await Create().CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task CheckNowAsync_ReturnsUpToDate_WhenOfferedUpdateIsADowngrade()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.5.0", isDowngrade: true));

        var outcome = await Create().CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task CheckNowAsync_ChecksStableOnly_WhenBetasAreOff()
    {
        _ = await Create().CheckNowAsync();

        Assert.Equal([UpdateChannel.Stable], _client.Checks);
    }

    [Fact]
    public async Task CheckNowAsync_ChecksBothChannels_WhenBetasAreOn()
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };

        _ = await Create().CheckNowAsync();

        Assert.Equal([UpdateChannel.Stable, UpdateChannel.Beta], _client.Checks);
    }

    [Fact]
    public async Task CheckNowAsync_ChecksStableWithDefaults_WhenSettingsCannotBeRead()
    {
        _store.LoadFailure = new IOException("locked");

        var outcome = await Create().CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpToDate, outcome);
        Assert.Equal([UpdateChannel.Stable], _client.Checks);
    }

    [Fact]
    public async Task CheckNowAsync_MovesFromCheckingToDownloadingToReadyToInstall_WhenAnUpdateIsFound()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var coordinator = Create();
        var states = new List<UpdateState>();
        coordinator.StateChanged += (_, _) => states.Add(coordinator.State);

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpdateFound, outcome);
        Assert.Equal(UpdateState.Downloading, coordinator.State);
        Assert.Equal("0.2.0", coordinator.ReadyVersion);

        _client.DownloadGate.SetResult();
        await coordinator.DownloadCompletion;

        Assert.Equal([UpdateState.Checking, UpdateState.Downloading, UpdateState.ReadyToInstall], states);
        Assert.Equal("0.2.0", coordinator.ReadyVersion);
        Assert.Equal(1, _client.Downloads);
    }

    [Fact]
    public async Task CheckNowAsync_ChecksOnce_WhenCallersOverlap()
    {
        var release = new TaskCompletionSource();
        _client.Checkers[UpdateChannel.Stable] = async _ =>
        {
            await release.Task;
            return null;
        };
        var coordinator = Create();

        var first = coordinator.CheckNowAsync();
        var second = coordinator.CheckNowAsync();
        Assert.Equal(UpdateState.Checking, coordinator.State);
        release.SetResult();

        Assert.Equal(UpdateCheckOutcome.UpToDate, await first);
        Assert.Equal(UpdateCheckOutcome.UpToDate, await second);
        Assert.Equal([UpdateChannel.Stable], _client.Checks);
    }

    [Fact]
    public async Task CheckNowAsync_DoesNotCheckOrDownloadAgain_WhenAnUpdateIsReady()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();
        await coordinator.DownloadCompletion;

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpdateFound, outcome);
        Assert.Equal(1, _client.Downloads);
        Assert.Single(_client.Checks);
    }

    [Fact]
    public async Task CheckNowAsync_DoesNotCheckAgain_WhileTheUpdateDownloads()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();

        var outcome = await coordinator.CheckNowAsync();

        Assert.Equal(UpdateCheckOutcome.UpdateFound, outcome);
        Assert.Single(_client.Checks);
        Assert.Equal(1, _client.Downloads);
        _client.DownloadGate.SetResult();
        await coordinator.DownloadCompletion;
    }

    [Fact]
    public async Task CheckNowAsync_GoesBackToIdleAndCanRetry_WhenTheDownloadFails()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadFailure = new IOException("disk full");
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();

        await coordinator.DownloadCompletion;

        Assert.Equal(UpdateState.Idle, coordinator.State);
        Assert.Null(coordinator.ReadyVersion);
        Assert.False(coordinator.ApplyPendingOnExit());
        Assert.Empty(_client.Applied);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);

        _client.DownloadFailure = null;
        _ = await coordinator.CheckNowAsync();
        await coordinator.DownloadCompletion;
        Assert.Equal(UpdateState.ReadyToInstall, coordinator.State);
        Assert.Equal(2, _client.Checks.Count);
    }

    [Fact]
    public async Task CheckNowAsync_ThrowsCanceledAndReturnsToIdle_WhenCancelled()
    {
        _client.Checkers[UpdateChannel.Stable] = async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        };
        var coordinator = Create();
        using var cts = new CancellationTokenSource();

        var check = coordinator.CheckNowAsync(cts.Token);
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check);
        Assert.Equal(UpdateState.Idle, coordinator.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApplyPendingOnExit_AppliesTheUpdateWithTheRestartFlag_WhenReady(bool restart)
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();
        await coordinator.DownloadCompletion;
        if (restart)
        {
            coordinator.RequestRestartOnExit();
        }

        var applied = coordinator.ApplyPendingOnExit();

        Assert.True(applied);
        var call = Assert.Single(_client.Applied);
        Assert.Equal("0.2.0", call.Candidate.Version.ToNormalizedString());
        Assert.Equal(restart, call.Restart);
    }

    [Fact]
    public async Task ApplyPendingOnExit_DoesNothing_WhenNoUpdateIsReady()
    {
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();

        Assert.False(coordinator.ApplyPendingOnExit());
        Assert.Empty(_client.Applied);
    }

    [Fact]
    public async Task ApplyPendingOnExit_DoesNothing_WhileTheUpdateIsStillDownloading()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var coordinator = Create();
        _ = await coordinator.CheckNowAsync();

        Assert.False(coordinator.ApplyPendingOnExit());

        Assert.Empty(_client.Applied);
        _client.DownloadGate.SetResult();
        await coordinator.DownloadCompletion;
    }

    [Fact]
    public void ApplyPendingOnExit_DoesNothing_WhenNotInstalled()
    {
        _client.Installed = false;

        Assert.False(Create().ApplyPendingOnExit());
        Assert.Empty(_client.Applied);
    }

    [Fact]
    public async Task CheckOnStartupAsync_IsReadyToInstall_WhenAnUpdateWasDownloadedEarlier()
    {
        _client.Pending = FakeUpdateClient.Candidate("0.2.0");
        _store.Current = new UserSettings { AutoCheckForUpdates = false };
        var coordinator = Create();

        await coordinator.CheckOnStartupAsync();

        Assert.Equal(UpdateState.ReadyToInstall, coordinator.State);
        Assert.Equal("0.2.0", coordinator.ReadyVersion);
        Assert.Empty(_client.Checks);
        Assert.True(coordinator.ApplyPendingOnExit());
    }

    [Fact]
    public async Task CheckOnStartupAsync_DoesNotCheckOrDownload_WhenAnUpdateWasDownloadedEarlier()
    {
        _client.Pending = FakeUpdateClient.Candidate("0.2.0");
        var coordinator = Create();

        await coordinator.CheckOnStartupAsync();

        Assert.Equal(UpdateState.ReadyToInstall, coordinator.State);
        Assert.Empty(_client.Checks);
        Assert.Equal(0, _client.Downloads);
    }

    [Fact]
    public async Task CheckOnStartupAsync_StaysIdle_WhenThePendingUpdateCannotBeRead()
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false };
        var client = new ThrowingPendingClient();
        var coordinator = new UpdateCoordinator(client, _store, Options.Create(new UpdateFeedOptions()), _logger);

        await coordinator.CheckOnStartupAsync();

        Assert.Equal(UpdateState.Idle, coordinator.State);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task CheckNowAsync_LogsNoVersionOrChannelBeyondNames_ForAFailedChannel()
    {
        _ = _client.Fails(UpdateChannel.Stable, new HttpRequestException("https://example.invalid/C:/Users/someone"));

        _ = await Create().CheckNowAsync();

        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("someone", StringComparison.Ordinal));
    }

    private sealed class ThrowingPendingClient : IUpdateClient
    {
        public bool IsInstalled => true;

        public Velopack.SemanticVersion? CurrentVersion => Velopack.SemanticVersion.Parse("0.1.0");

        public UpdateCandidate? PendingRestart => throw new IOException("packages folder unreadable");

        public Task<UpdateCandidate?> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken) => Task.FromResult<UpdateCandidate?>(null);

        public Task DownloadAsync(UpdateCandidate candidate, CancellationToken cancellationToken) => Task.CompletedTask;

        public void ApplyOnExit(UpdateCandidate candidate, bool restart)
        {
        }
    }
}
