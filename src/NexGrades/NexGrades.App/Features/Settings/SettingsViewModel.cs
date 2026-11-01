using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.App.Core;
using NexGrades.Common.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// View model of the settings page. It lets the user pick a folder. For now the selection is only shown; it is
/// not saved.
/// </summary>
/// <param name="dialogService">Opens the folder picker.</param>
/// <param name="snackbarService">Shows a caution message when no folder is selected.</param>
public partial class SettingsViewModel(IFileSystemDialogService dialogService, ISnackbarService snackbarService) : ViewModel
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
}
