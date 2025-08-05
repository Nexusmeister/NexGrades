using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace NexGrades.App.ViewModels;

public partial class SettingsViewModel : ViewModel
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

        OpenFolderDialog openFolderDialog = new()
        {
            Multiselect = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (openFolderDialog.ShowDialog() != true)
        {
            return;
        }

        if (openFolderDialog.FolderNames.Length == 0)
        {
            return;
        }

        OpenedFolderPath = string.Join("\n", openFolderDialog.FolderNames);
        OpenedFolderPathVisibility = true;
    }
}