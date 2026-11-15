using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Infrastructure;
using NexGrades.Common.Services;
using NexGrades.Common.Storage;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// View model of the settings page. It lets the user pick a folder. For now the selection is only shown; it is
/// not saved.
/// </summary>
/// <param name="dialogService">Opens the folder picker.</param>
/// <param name="snackbarService">Shows a caution message when no folder is selected and reports a failed folder open.</param>
/// <param name="appDataPaths">Locates the log folder.</param>
/// <param name="folderOpener">Shows the log folder in the file browser.</param>
/// <param name="logger">Records a failure to open the log folder.</param>
public partial class SettingsViewModel(
    IFileSystemDialogService dialogService,
    ISnackbarService snackbarService,
    IAppDataPaths appDataPaths,
    IFolderOpener folderOpener,
    ILogger<SettingsViewModel> logger) : ViewModel
{
    [ObservableProperty]
    private bool _openedFolderPathVisibility;

    [ObservableProperty]
    private string _openedFolderPath = string.Empty;

    /// <summary>
    /// Opens the folder picker and shows the chosen path. Cancelling, or selecting nothing, hides the path and shows
    /// a caution message instead.
    /// </summary>
    [RelayCommand]
    public void OnOpenFolder()
    {
        OpenedFolderPathVisibility = false;

        // Only one folder can be used for now; selecting several is not supported yet (#83).
        var foldersSelected = dialogService.SelectFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)).ToList();

        if (foldersSelected.Count == 0)
        {
            snackbarService.Show(
                "Missing setting",
                "Save location has to be selected. Rolling back to default save location in My Documents.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.FolderLightning20),
                TimeSpan.FromSeconds(15));
            return;
        }

        OpenedFolderPath = string.Join("\n", foldersSelected);
        OpenedFolderPathVisibility = true;
    }

    /// <summary>
    /// Opens the log folder in the file browser, creating it first when logging has not yet. Failure is reported in
    /// a snackbar; the path itself is not logged, because it carries the Windows user name.
    /// </summary>
    [RelayCommand]
    public void OpenLogFolder()
    {
        try
        {
            var directory = appDataPaths.LogDirectory;
            _ = Directory.CreateDirectory(directory);
            folderOpener.Open(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogOpenLogFolderFailed(ex);
            snackbarService.Show(
                "Error",
                "The log folder could not be opened.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The log folder could not be opened.")]
    private partial void LogOpenLogFolderFailed(Exception exception);
}
