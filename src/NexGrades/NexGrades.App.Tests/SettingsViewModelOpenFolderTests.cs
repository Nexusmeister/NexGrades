using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Features.Settings;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;

namespace NexGrades.App.Tests;

/// <summary>Tests the folder picker command of <see cref="SettingsViewModel"/>: none, one and several folders (#83).</summary>
public sealed class SettingsViewModelOpenFolderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private sealed class FakeDialogService(params string[] folders) : IFileSystemDialogService
    {
        public IEnumerable<string> SelectFolder(string basePath = "") => folders;
    }

    private sealed class NoOpFolderOpener : IFolderOpener
    {
        public void Open(string path)
        {
        }
    }

    private (SettingsViewModel ViewModel, FakeSnackbar Snackbar) Create(params string[] folders)
    {
        var (snackbar, fake) = FakeSnackbar.Create();
        var viewModel = new SettingsViewModel(
            new FakeDialogService(folders),
            snackbar,
            new AppDataPaths(_folder.Path),
            new NoOpFolderOpener(),
            NullLogger<SettingsViewModel>.Instance,
            null!,
            null!);
        return (viewModel, fake);
    }

    [Fact]
    public Task OnOpenFolder_ShowsMissingSnackbarAndHidesPath_WhenNoFolderIsSelectedAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, snackbar) = Create();
        viewModel.OpenedFolderPathVisibility = true;

        viewModel.OnOpenFolder();

        Assert.Equal(["Missing setting"], snackbar.Titles);
        Assert.False(viewModel.OpenedFolderPathVisibility);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OnOpenFolder_ShowsPathWithoutSnackbar_WhenOneFolderIsSelectedAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, snackbar) = Create(@"C:\Chosen\Folder");

        viewModel.OnOpenFolder();

        Assert.Equal(@"C:\Chosen\Folder", viewModel.OpenedFolderPath);
        Assert.True(viewModel.OpenedFolderPathVisibility);
        Assert.Empty(snackbar.Titles);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OnOpenFolder_ShowsRejectionSnackbarAndHidesPath_WhenTwoFoldersAreSelectedAsync() => StaTestRunner.RunAsync(() =>
    {
        var (viewModel, snackbar) = Create(@"C:\First", @"C:\Second");
        viewModel.OpenedFolderPathVisibility = true;

        viewModel.OnOpenFolder();

        Assert.Equal(["Too many folders"], snackbar.Titles);
        Assert.False(viewModel.OpenedFolderPathVisibility);
        Assert.Equal(string.Empty, viewModel.OpenedFolderPath);
        return Task.CompletedTask;
    });
}
