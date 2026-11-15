using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
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

    private (SettingsViewModel ViewModel, FakeFolderOpener Opener, FakeSnackbar Snackbar, AppDataPaths Paths) Create(Exception? openerFailure = null)
    {
        var paths = new AppDataPaths(_folder.Path);
        var opener = new FakeFolderOpener(openerFailure);
        var (snackbar, fake) = FakeSnackbar.Create();
        var viewModel = new SettingsViewModel(null!, snackbar, paths, opener, NullLogger<SettingsViewModel>.Instance);
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
}
