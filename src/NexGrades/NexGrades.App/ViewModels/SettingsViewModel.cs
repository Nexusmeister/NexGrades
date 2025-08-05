using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Windows;

namespace NexGrades.App.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    private bool _isInitialized = false;

    [ObservableProperty]
    private Visibility _openedFolderPathVisibility = Visibility.Collapsed;
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
        OpenedFolderPathVisibility = Visibility.Collapsed;

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
        OpenedFolderPathVisibility = Visibility.Visible;
    }
}