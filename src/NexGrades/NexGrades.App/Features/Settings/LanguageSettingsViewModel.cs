using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using NexGrades.App.Localization;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Settings;

/// <summary>
/// The "Language" section of the settings page. A choice is saved as soon as it is made but applies only after a
/// restart, so <see cref="IsRestartPending"/> tells the page to show the restart notice. Exposed as
/// <c>SettingsViewModel.Language</c>.
/// </summary>
/// <param name="languageService">Saves the preference and knows whether a restart is pending.</param>
/// <param name="snackbar">Reports a preference that could not be saved.</param>
public sealed partial class LanguageSettingsViewModel(AppLanguageService languageService, ISnackbarService snackbar) : ObservableObject
{
    // Set while LoadAsync shows the saved preference, so that does not write it straight back.
    private bool _loading;

    private Task _saveChain = Task.CompletedTask;

    /// <summary>
    /// The language choices, in display order. The names of the two languages are the same in every UI language, so a
    /// user who cannot read the current one can still find theirs.
    /// </summary>
    public IReadOnlyList<LanguageOption> Options { get; } =
    [
        new(LanguagePreference.System, TextResources.Text("Settings_Language_System")),
        new(LanguagePreference.English, TextResources.Text("Settings_Language_English")),
        new(LanguagePreference.German, TextResources.Text("Settings_Language_German")),
    ];

    /// <summary>The chosen language. Changing it saves it; it applies after a restart.</summary>
    [ObservableProperty]
    private LanguagePreference _selectedLanguage = LanguagePreference.System;

    /// <summary>Whether the saved language differs from the one in use, so a restart is needed.</summary>
    [ObservableProperty]
    private bool _isRestartPending;

    /// <summary>Shows the saved preference; called when the settings page opens. It writes nothing.</summary>
    public Task LoadAsync()
    {
        _loading = true;
        try
        {
            SelectedLanguage = languageService.Saved;
            IsRestartPending = languageService.IsRestartPending;
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

    partial void OnSelectedLanguageChanged(LanguagePreference value)
    {
        if (!_loading)
        {
            _saveChain = SaveAfterAsync(_saveChain, value);
        }
    }

    // Each save waits for the previous one, so quick selections reach the file in order and it ends matching the last.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Runs unobserved after a selection; a failure is reported in a snackbar and must never go unhandled.")]
    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The previous task is this view model's own save, which never faults; awaiting it keeps the saves in order.")]
    private async Task SaveAfterAsync(Task previous, LanguagePreference value)
    {
        await previous;

        var saved = false;
        try
        {
            saved = await languageService.SetAsync(value);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Saving the language failed: {ex.GetType().Name}");
        }

        IsRestartPending = languageService.IsRestartPending;
        if (saved)
        {
            return;
        }

        try
        {
            snackbar.Show(
                TextResources.Text("Settings_Language_SaveFailed_Title"),
                TextResources.Text("Settings_Language_SaveFailed_Message"),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            // Nothing is left to report to, and a fault here would break the chain.
            Debug.WriteLine($"Reporting the language save failure failed: {ex.GetType().Name}");
        }
    }
}
