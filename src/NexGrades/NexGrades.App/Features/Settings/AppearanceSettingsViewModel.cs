using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    private Task _saveChain = Task.CompletedTask;

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

    /// <summary>The latest save. It completes after every earlier one, and never faults; tests await it.</summary>
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The chain is this view model's own saves and never faults; it is exposed so tests can await them.")]
    internal Task PendingSave => _saveChain;

    partial void OnSelectedThemeChanged(ThemePreference value)
    {
        if (!_loading)
        {
            _saveChain = SaveAfterAsync(_saveChain, value);
        }
    }

    // Each save waits for the previous one, so quick selections reach the file in order and it ends matching the last.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Runs unobserved after a selection; a failure is reported in a snackbar and must never go unhandled.")]
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The previous task is this view model's own save, which never faults; awaiting it keeps the saves in order.")]
    private async Task SaveAfterAsync(Task previous, ThemePreference value)
    {
        await previous;

        var saved = false;
        try
        {
            saved = await themeService.SetAsync(value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Saving the theme failed: {ex.GetType().Name}");
        }

        if (saved)
        {
            return;
        }

        try
        {
            snackbar.Show(
                "Error",
                "The theme could not be saved. It applies until you close NexGrades.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            // Nothing is left to report to, and a fault here would break the chain; the theme still applies until the app closes.
            Debug.WriteLine($"Reporting the theme save failure failed: {ex.GetType().Name}");
        }
    }
}
