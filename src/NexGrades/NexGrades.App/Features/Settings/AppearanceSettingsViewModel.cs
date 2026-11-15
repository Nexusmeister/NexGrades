using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Theming;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// The "Appearance" section of the settings page: the colour theme. A choice applies and is saved as soon as it is
/// made. Exposed as <c>SettingsViewModel.Appearance</c>.
/// </summary>
/// <param name="themeService">Applies and saves the preference.</param>
/// <param name="snackbar">Reports a preference that could not be saved.</param>
public sealed partial class AppearanceSettingsViewModel(AppThemeService themeService, ISnackbarService snackbar) : ObservableObject
{
    // Set while LoadAsync shows the current preference, so that does not write it straight back.
    private bool _loading;

    /// <summary>The theme choices, in display order.</summary>
    public IReadOnlyList<ThemeOption> Options { get; } =
    [
        new(ThemePreference.System, "System"),
        new(ThemePreference.Light, "Light"),
        new(ThemePreference.Dark, "Dark"),
    ];

    /// <summary>The chosen theme. Changing it applies and saves it.</summary>
    [ObservableProperty]
    private ThemePreference _selectedTheme = ThemePreference.System;

    /// <summary>Shows the preference currently in effect; called when the settings page opens. It writes nothing.</summary>
    public Task LoadAsync()
    {
        _loading = true;
        try
        {
            SelectedTheme = themeService.Current;
        }
        finally
        {
            _loading = false;
        }

        return Task.CompletedTask;
    }

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (!_loading)
        {
            _ = SaveAsync(value);
        }
    }

    private async Task SaveAsync(ThemePreference value)
    {
        if (await themeService.SetAsync(value))
        {
            return;
        }

        snackbar.Show(
            "Error",
            "The theme could not be saved. It applies until you close NexGrades.",
            ControlAppearance.Caution,
            new SymbolIcon(SymbolRegular.ErrorCircle24),
            TimeSpan.FromSeconds(5));
    }
}
