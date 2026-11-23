using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Infrastructure;
using NexGrades.App.Storage;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// View model of the settings page. It lets the user pick a folder (for now the selection is only shown; it is
/// not saved), opens the log folder, and holds the update section in <see cref="Updates"/>.
/// </summary>
/// <param name="dialogService">Opens the folder picker.</param>
/// <param name="snackbarService">Shows a caution message when no folder is selected and reports a failed folder open.</param>
/// <param name="appDataPaths">Locates the log folder.</param>
/// <param name="folderOpener">Shows the log folder in the file browser.</param>
/// <param name="logger">Records a failure to open the log folder.</param>
/// <param name="updates">The update section of the page.</param>
/// <param name="appearance">The appearance section of the page.</param>
public partial class SettingsViewModel(
    IFileSystemDialogService dialogService,
    ISnackbarService snackbarService,
    IAppDataPaths appDataPaths,
    IFolderOpener folderOpener,
    ILogger<SettingsViewModel> logger,
    UpdateSettingsViewModel updates,
    AppearanceSettingsViewModel appearance) : ViewModel
{
    /// <summary>The update section: version, update preferences, "Check for updates now" and "Restart now".</summary>
    public UpdateSettingsViewModel Updates { get; } = updates;

    /// <summary>The appearance section: the colour theme.</summary>
    public AppearanceSettingsViewModel Appearance { get; } = appearance;

    [ObservableProperty]
    private bool _openedFolderPathVisibility;

    [ObservableProperty]
    private string _openedFolderPath = string.Empty;

    /// <summary>Reloads the stored update and appearance preferences each time the page is shown.</summary>
    /// <param name="parameter">Not used; the settings page takes no parameter.</param>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        await base.OnNavigatedToAsync(parameter);
        await Updates.LoadAsync();
        await Appearance.LoadAsync();
    }

    /// <summary>
    /// Opens the folder picker and shows the chosen path. Cancelling, or selecting nothing, hides the path and shows
    /// a caution message instead; so does a result of more than one folder, none of which is used.
    /// </summary>
    [RelayCommand]
    public void OnOpenFolder()
    {
        OpenedFolderPathVisibility = false;

        var foldersSelected = dialogService.SelectFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)).ToList();

        // The dialog allows a single folder; if an implementation still returns several, none is used.
        if (foldersSelected.Count > 1)
        {
            snackbarService.Show(
                "Too many folders",
                "Select a single folder.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.FolderLightning20),
                TimeSpan.FromSeconds(5));
            return;
        }

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

        OpenedFolderPath = foldersSelected[0];
        OpenedFolderPathVisibility = true;
    }

    /// <summary>
    /// Opens the log folder in the file browser, creating it first when logging has not yet. Failure is reported in
    /// a snackbar. The log entry carries a constant message only and not the exception, because the exception text can
    /// contain the path, which carries the Windows user name.
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
            LogOpenLogFolderFailed();
            snackbarService.Show(
                "Error",
                "The log folder could not be opened.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The log folder could not be opened.")]
    private partial void LogOpenLogFolderFailed();
}
