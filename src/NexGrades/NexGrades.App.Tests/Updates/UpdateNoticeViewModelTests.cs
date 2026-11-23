using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexGrades.App.Core;
using NexGrades.App.Infrastructure;
using NexGrades.App.Tests.Core;
using NexGrades.App.Tests.TestSupport;
using NexGrades.App.Updates;

namespace NexGrades.App.Tests.Updates;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await gates they own and release themselves.")]
public class UpdateNoticeViewModelTests
{

    private static string Fmt(string format, string value) => format.Replace("{0}", value, StringComparison.Ordinal);
    private readonly FakeUpdateClient _client = new();
    private readonly FakeUserSettingsStore _store = new() { Current = new UserSettings { AutoCheckForUpdates = false } };
    private readonly FakeAppShutdown _shutdown = new();
    private readonly PageNavigator _navigator = new(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
    private readonly UpdateCoordinator _coordinator;

    public UpdateNoticeViewModelTests()
    {
        _coordinator = new UpdateCoordinator(_client, _store, Options.Create(new UpdateFeedOptions()), NullLogger<UpdateCoordinator>.Instance);
    }

    private UpdateNoticeViewModel Create() => new(_coordinator, _navigator, _shutdown, action => action());

    private async Task MakeReadyAsync()
    {
        _client.Pending = FakeUpdateClient.Candidate("0.2.0");
        await _coordinator.CheckOnStartupAsync();
    }

    [Fact]
    public void Constructor_IsClosed_WhenNoUpdateIsReady()
    {
        var notice = Create();

        Assert.False(notice.IsOpen);
        Assert.Equal(string.Empty, notice.Message);
    }

    [Fact]
    public async Task IsOpen_BecomesTrueWithFormattedMessage_WhenAnUpdateIsReady()
    {
        var notice = Create();

        await MakeReadyAsync();

        Assert.True(notice.IsOpen);
        Assert.Equal(Fmt(UpdateNoticeViewModel.ReadyMessageFormat, "0.2.0"), notice.Message);
    }

    [Fact]
    public async Task Constructor_IsOpen_WhenAnUpdateIsAlreadyReady()
    {
        await MakeReadyAsync();

        var notice = Create();

        Assert.True(notice.IsOpen);
        Assert.Contains("0.2.0", notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IsOpen_StaysFalse_WhileTheUpdateIsStillDownloading()
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var notice = Create();

        _ = await _coordinator.CheckNowAsync();

        Assert.False(notice.IsOpen);
        _client.DownloadGate.SetResult();
        await _coordinator.DownloadCompletion;
        Assert.True(notice.IsOpen);
    }

    [Fact]
    public async Task RestartNowCommand_DoesNotShutDownOrRequestRestart_WhenThePageRefusesToBeLeft()
    {
        await MakeReadyAsync();
        var page = new PageNavigatorGuardTests.GuardedViewModel();
        page.Gate.SetResult(false);
        _navigator.SetCurrent(page);
        var notice = Create();

        await notice.RestartNowCommand.ExecuteAsync(null);

        Assert.Equal(0, _shutdown.Calls);
        Assert.True(_coordinator.ApplyPendingOnExit());
        Assert.False(Assert.Single(_client.Applied).Restart);
    }

    [Fact]
    public async Task RestartNowCommand_RequestsRestartAndShutsDown_WhenThePageAllowsLeaving()
    {
        await MakeReadyAsync();
        var page = new PageNavigatorGuardTests.GuardedViewModel();
        page.Gate.SetResult(true);
        _navigator.SetCurrent(page);
        var notice = Create();

        await notice.RestartNowCommand.ExecuteAsync(null);

        Assert.Equal(1, _shutdown.Calls);
        Assert.True(_coordinator.ApplyPendingOnExit());
        Assert.True(Assert.Single(_client.Applied).Restart);
    }

    [Fact]
    public async Task RestartNowCommand_ShutsDown_WhenThereIsNoCurrentPage()
    {
        await MakeReadyAsync();
        _navigator.SetCurrent(null);
        var notice = Create();

        await notice.RestartNowCommand.ExecuteAsync(null);

        Assert.Equal(1, _shutdown.Calls);
    }

    [Fact]
    public async Task RestartNowCommand_DoesNotShutDown_WhenThePageCheckThrows()
    {
        await MakeReadyAsync();
        var page = new PageNavigatorGuardTests.GuardedViewModel();
        page.Gate.SetException(new InvalidOperationException("boom"));
        _navigator.SetCurrent(page);
        var notice = Create();

        await notice.RestartNowCommand.ExecuteAsync(null);

        Assert.Equal(0, _shutdown.Calls);
    }
}
