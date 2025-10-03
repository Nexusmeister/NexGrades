using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexGrades.App.Core;
using NexGrades.Common.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

public partial class SettingsViewModel(IFileSystemDialogService dialogService, ISnackbarService snackbarService) : ViewModel
{
    private bool _isInitialized = false;

    [ObservableProperty]
    private bool _openedFolderPathVisibility;

    [ObservableProperty]
    private string _openedFolderPath = string.Empty;

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            //InitializeViewModel();
        }
    }

    [RelayCommand]
    public void OnOpenFolder()
    {
        OpenedFolderPathVisibility = false;

        // TODO Multiple folders are not allowed!
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