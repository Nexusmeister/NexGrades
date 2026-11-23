using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexGrades.App.Core;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.App.Tests.TestSupport;
using NexGrades.App.Updates;

namespace NexGrades.App.Tests.Features.Settings;

[SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "Tests await gates they own and release themselves.")]
public class UpdateSettingsViewModelTests
{

    private static string Fmt(string format, string value) => format.Replace("{0}", value, StringComparison.Ordinal);
    private readonly FakeUpdateClient _client = new();
    private readonly FakeUserSettingsStore _store = new();
    private readonly FakeAppShutdown _shutdown = new();
    private readonly PageNavigator _navigator = new(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
    private readonly FakeSnackbar _snackbar;
    private readonly Wpf.Ui.ISnackbarService _snackbarService;
    private UpdateCoordinator? _coordinator;

    public UpdateSettingsViewModelTests()
    {
        (_snackbarService, _snackbar) = FakeSnackbar.Create();
    }

    // Created on first use, so a test can change the fake client before the coordinator reads the install state.
    private UpdateCoordinator Coordinator => _coordinator ??= new UpdateCoordinator(_client, _store, Options.Create(new UpdateFeedOptions()), NullLogger<UpdateCoordinator>.Instance);

    private UpdateSettingsViewModel Create()
    {
        var notice = new UpdateNoticeViewModel(Coordinator, _navigator, _shutdown, action => action());
        return new UpdateSettingsViewModel(Coordinator, _store, notice, _snackbarService, NullLogger<UpdateSettingsViewModel>.Instance, action => action());
    }

    [Fact]
    public Task LoadAsync_ShowsTheStoredToggles() => StaTestRunner.RunAsync(async () =>
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false, ReceiveBetaUpdates = true };
        var viewModel = Create();

        await viewModel.LoadAsync();

        Assert.False(viewModel.AutoCheckForUpdates);
        Assert.True(viewModel.ReceiveBetaUpdates);
    });

    [Fact]
    public Task LoadAsync_DoesNotSaveAnything() => StaTestRunner.RunAsync(async () =>
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false, ReceiveBetaUpdates = true };
        var viewModel = Create();

        await viewModel.LoadAsync();

        Assert.Equal(0, _store.UpdateCalls);
        Assert.Empty(_snackbar.Titles);
    });

    [Fact]
    public Task LoadAsync_KeepsTheToggles_WhenTheSettingsCannotBeRead() => StaTestRunner.RunAsync(async () =>
    {
        _store.LoadFailure = new IOException("locked");
        var viewModel = Create();

        var exception = await Record.ExceptionAsync(viewModel.LoadAsync);

        Assert.Null(exception);
        Assert.True(viewModel.AutoCheckForUpdates);
        Assert.False(viewModel.ReceiveBetaUpdates);
        Assert.Equal(0, _store.UpdateCalls);
    });

    [Fact]
    public Task AutoCheckForUpdates_IsSavedWithoutTouchingTheOtherSettings_WhenChanged() => StaTestRunner.RunAsync(async () =>
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true, LastRunVersion = "0.1.0" };
        var viewModel = Create();
        await viewModel.LoadAsync();

        viewModel.AutoCheckForUpdates = false;

        Assert.Equal(1, _store.UpdateCalls);
        Assert.False(_store.Current.AutoCheckForUpdates);
        Assert.True(_store.Current.ReceiveBetaUpdates);
        Assert.Equal("0.1.0", _store.Current.LastRunVersion);
    });

    [Fact]
    public Task ReceiveBetaUpdates_IsSavedWithoutTouchingTheOtherSettings_WhenChanged() => StaTestRunner.RunAsync(async () =>
    {
        _store.Current = new UserSettings { AutoCheckForUpdates = false, LastRunVersion = "0.1.0" };
        var viewModel = Create();
        await viewModel.LoadAsync();

        viewModel.ReceiveBetaUpdates = true;

        Assert.Equal(1, _store.UpdateCalls);
        Assert.True(_store.Current.ReceiveBetaUpdates);
        Assert.False(_store.Current.AutoCheckForUpdates);
        Assert.Equal("0.1.0", _store.Current.LastRunVersion);
    });

    [Fact]
    public Task ReceiveBetaUpdates_IsSavedOff_WhenTurnedBackOff() => StaTestRunner.RunAsync(async () =>
    {
        _store.Current = new UserSettings { ReceiveBetaUpdates = true };
        var viewModel = Create();
        await viewModel.LoadAsync();

        viewModel.ReceiveBetaUpdates = false;

        Assert.False(_store.Current.ReceiveBetaUpdates);
    });

    [Fact]
    public Task ToggleChange_ReportsThroughTheSnackbar_WhenSavingFails() => StaTestRunner.RunAsync(async () =>
    {
        var viewModel = Create();
        await viewModel.LoadAsync();
        _store.UpdateFailure = new IOException("read-only");

        var exception = Record.Exception(() => viewModel.ReceiveBetaUpdates = true);

        Assert.Null(exception);
        Assert.Equal(["Error"], _snackbar.Titles);
        Assert.DoesNotContain("read-only", _snackbar.Messages[0], StringComparison.Ordinal);
    });

    [Fact]
    public Task VersionText_ShowsTheRunningVersion() => StaTestRunner.RunAsync(() =>
    {
        _client.Current = Velopack.SemanticVersion.Parse("0.3.1");

        var viewModel = Create();

        Assert.Equal(Fmt(TextResources.Text("Settings_Updates_Version_Format"), "0.3.1"), viewModel.VersionText);
        return Task.CompletedTask;
    });

    [Fact]
    public Task VersionText_ShowsTheBuildVersion_WhenNotInstalled() => StaTestRunner.RunAsync(() =>
    {
        _client.Installed = false;

        var viewModel = Create();

        Assert.Equal(Fmt(TextResources.Text("Settings_Updates_Version_Format"), AppVersion.Current), viewModel.VersionText);
        return Task.CompletedTask;
    });

    [Fact]
    public Task CheckNowCommand_ShowsNotInstalledText_WhenNotInstalled() => StaTestRunner.RunAsync(async () =>
    {
        _client.Installed = false;
        var viewModel = Create();

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsUpdaterAvailable);
        Assert.Equal(TextResources.Text("Settings_Updates_NotInstalled"), viewModel.StatusText);
        Assert.Empty(_client.Checks);
        Assert.False(viewModel.IsChecking);
    });

    [Fact]
    public Task CheckNowCommand_ShowsUpToDateText_WhenNothingIsNewer() => StaTestRunner.RunAsync(async () =>
    {
        var viewModel = Create();

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsUpdaterAvailable);
        Assert.Equal(TextResources.Text("Settings_Updates_UpToDate"), viewModel.StatusText);
        Assert.False(viewModel.IsReadyToInstall);
    });

    [Fact]
    public Task CheckNowCommand_ShowsOfflineText_WhenNoChannelCanBeChecked() => StaTestRunner.RunAsync(async () =>
    {
        _ = _client.Fails(UpdateChannel.Stable);
        var viewModel = Create();

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.Equal(TextResources.Text("Settings_Updates_Offline"), viewModel.StatusText);
    });

    [Fact]
    public Task CheckNowCommand_ShowsDownloadingThenReadyText_WhenAnUpdateIsFound() => StaTestRunner.RunAsync(async () =>
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var viewModel = Create();

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.Equal(Fmt(TextResources.Text("Settings_Updates_Downloading_Format"), "0.2.0"), viewModel.StatusText);
        Assert.False(viewModel.IsReadyToInstall);

        _client.DownloadGate.SetResult();
        await Coordinator.DownloadCompletion;

        Assert.Equal(Fmt(TextResources.Text("Shell_UpdateNotice_Ready_Format"), "0.2.0"), viewModel.StatusText);
        Assert.True(viewModel.IsReadyToInstall);
    });

    [Fact]
    public Task StatusText_IsCleared_WhenTheDownloadFails() => StaTestRunner.RunAsync(async () =>
    {
        _ = _client.Returns(UpdateChannel.Stable, FakeUpdateClient.Candidate("0.2.0"));
        _client.DownloadGate = new TaskCompletionSource();
        var viewModel = Create();
        await viewModel.CheckNowCommand.ExecuteAsync(null);

        _client.DownloadFailure = new IOException("disk full");
        _client.DownloadGate.SetResult();
        await Coordinator.DownloadCompletion;

        Assert.Equal(string.Empty, viewModel.StatusText);
        Assert.False(viewModel.IsReadyToInstall);
    });

    [Fact]
    public Task IsReadyToInstall_IsTrue_WhenAnUpdateWasDownloadedEarlier() => StaTestRunner.RunAsync(async () =>
    {
        _client.Pending = FakeUpdateClient.Candidate("0.2.0");
        _store.Current = new UserSettings { AutoCheckForUpdates = false };
        var viewModel = Create();

        await Coordinator.CheckOnStartupAsync();

        Assert.True(viewModel.IsReadyToInstall);
        Assert.Equal(Fmt(TextResources.Text("Shell_UpdateNotice_Ready_Format"), "0.2.0"), viewModel.StatusText);
    });

    [Fact]
    public Task CheckNowCommand_IsBusyAndCannotRunAgain_WhileChecking() => StaTestRunner.RunAsync(async () =>
    {
        var release = new TaskCompletionSource();
        _client.Checkers[UpdateChannel.Stable] = async _ =>
        {
            await release.Task;
            return null;
        };
        var viewModel = Create();

        var running = viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsChecking);
        Assert.False(viewModel.CheckNowCommand.CanExecute(null));

        release.SetResult();
        await running;

        Assert.False(viewModel.IsChecking);
        Assert.True(viewModel.CheckNowCommand.CanExecute(null));
        Assert.Single(_client.Checks);
    });

    [Fact]
    public Task CheckNowCommand_ShowsOfflineAndStopsChecking_WhenTheChannelCheckIsAborted() => StaTestRunner.RunAsync(async () =>
    {
        _client.Checkers[UpdateChannel.Stable] = _ => throw new OperationCanceledException();
        var viewModel = Create();

        await viewModel.CheckNowCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsChecking);
        Assert.Equal(TextResources.Text("Settings_Updates_Offline"), viewModel.StatusText);
    });

    [Fact]
    public Task RestartNowCommand_IsTheNoticeCommand() => StaTestRunner.RunAsync(() =>
    {
        var notice = new UpdateNoticeViewModel(Coordinator, _navigator, _shutdown, action => action());
        var viewModel = new UpdateSettingsViewModel(Coordinator, _store, notice, _snackbarService, NullLogger<UpdateSettingsViewModel>.Instance, action => action());

        Assert.Same(notice.RestartNowCommand, viewModel.RestartNowCommand);
        return Task.CompletedTask;
    });

    [Fact]
    public Task StatusText_IsGerman_ForGermanUiCulture() => StaTestRunner.RunAsync(async () =>
    {
        var ui = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            _client.Installed = false;
            var viewModel = Create();

            await viewModel.CheckNowCommand.ExecuteAsync(null);

            Assert.Equal("Updates sind nur in der installierten App verfügbar.", viewModel.StatusText);
        }
        finally
        {
            CultureInfo.CurrentUICulture = ui;
        }
    });
}
