using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NexGrades.App.Core;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Updates;
using NexGrades.Common.Storage;

namespace NexGrades.App.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private sealed class FakeFolderOpener(Exception? toThrow = null) : IFolderOpener
    {
        public List<string> Opened { get; } = [];

        public void Open(string path)
        {
            if (toThrow is not null)
            {
                throw toThrow;
            }

            Opened.Add(path);
        }
    }

    private (SettingsViewModel ViewModel, FakeFolderOpener Opener, FakeSnackbar Snackbar, AppDataPaths Paths) Create(Exception? openerFailure = null, UpdateSettingsViewModel? updates = null)
    {
        var paths = new AppDataPaths(_folder.Path);
        var opener = new FakeFolderOpener(openerFailure);
        var (snackbar, fake) = FakeSnackbar.Create();
        var viewModel = new SettingsViewModel(null!, snackbar, paths, opener, NullLogger<SettingsViewModel>.Instance, updates!);
        return (viewModel, opener, fake, paths);
    }

    [Fact]
    public Task OpenLogFolder_CreatesTheMissingFolderAndOpensItAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, opener, snackbar, paths) = Create();
        Assert.False(Directory.Exists(paths.LogDirectory));

        viewModel.OpenLogFolderCommand.Execute(null);

        Assert.True(Directory.Exists(paths.LogDirectory));
        Assert.Equal([paths.LogDirectory], opener.Opened);
        Assert.Empty(snackbar.Titles);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenLogFolder_OpensTheExistingFolder_WhenItAlreadyExistsAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, opener, snackbar, paths) = Create();
        _ = Directory.CreateDirectory(paths.LogDirectory);

        viewModel.OpenLogFolderCommand.Execute(null);

        Assert.Equal([paths.LogDirectory], opener.Opened);
        Assert.Empty(snackbar.Titles);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenLogFolder_ReportsThroughTheSnackbar_WhenTheOpenerThrowsIOExceptionAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, opener, snackbar, _) = Create(new IOException("boom"));

        var exception = Record.Exception(() => viewModel.OpenLogFolderCommand.Execute(null));

        Assert.Null(exception);
        Assert.Empty(opener.Opened);
        Assert.Equal(["Error"], snackbar.Titles);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OpenLogFolder_ReportsThroughTheSnackbar_WhenTheFolderCannotBeCreatedAsync() => StaTestRunner.RunAsync(async () =>
    {
        var (viewModel, opener, snackbar, paths) = Create();
        // A file where the "logs" folder should be makes CreateDirectory fail.
        await File.WriteAllTextAsync(paths.LogDirectory, "x");

        var exception = Record.Exception(() => viewModel.OpenLogFolderCommand.Execute(null));

        Assert.Null(exception);
        Assert.Empty(opener.Opened);
        Assert.Equal(["Error"], snackbar.Titles);
    });

    [Fact]
    public Task OnNavigatedToAsync_LoadsTheStoredUpdatePreferences_WhenThePageOpens() => StaTestRunner.RunAsync(async () =>
    {
        var store = new FakeUserSettingsStore { Current = new UserSettings { AutoCheckForUpdates = false, ReceiveBetaUpdates = true } };
        var coordinator = new UpdateCoordinator(new FakeUpdateClient(), store, Options.Create(new UpdateFeedOptions()), NullLogger<UpdateCoordinator>.Instance);
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var notice = new UpdateNoticeViewModel(coordinator, navigator, new FakeAppShutdown(), action => action());
        var (snackbar, _) = FakeSnackbar.Create();
        var updates = new UpdateSettingsViewModel(coordinator, store, notice, snackbar, NullLogger<UpdateSettingsViewModel>.Instance, action => action());
        var (viewModel, _, _, _) = Create(updates: updates);

        await viewModel.OnNavigatedToAsync();

        Assert.Same(updates, viewModel.Updates);
        Assert.False(updates.AutoCheckForUpdates);
        Assert.True(updates.ReceiveBetaUpdates);
        Assert.Equal(0, store.UpdateCalls);
    });
}
