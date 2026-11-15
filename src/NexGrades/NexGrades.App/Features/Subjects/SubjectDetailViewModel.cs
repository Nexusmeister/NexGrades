using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Subjects;

/// <summary>Form for adding a subject to the catalogue.</summary>
/// <param name="navigation">Navigates back after a save or a cancel.</param>
/// <param name="snackbar">Reports the outcome of a save.</param>
/// <param name="subjectService">Validates and saves the subject.</param>
/// <param name="contentDialogService">Asks before unsaved input is discarded.</param>
public partial class SubjectDetailViewModel(INavigationService navigation, ISnackbarService snackbar, SubjectService subjectService, IContentDialogService contentDialogService) : ViewModel
{
    private readonly LeaveGuard _leaveGuard = new(contentDialogService);

    [ObservableProperty] private string _title = "AddSubject";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _shortCode = string.Empty;

    /// <summary>Longest subject name the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxNameLength { get; } = SubjectService.MaxNameLength;

    /// <summary>Longest short code the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxShortCodeLength { get; } = SubjectService.MaxShortCodeLength;

    /// <summary>True when the name or the short code has been typed into.</summary>
    private bool HasUnsavedChanges => Name.Trim().Length > 0 || ShortCode.Trim().Length > 0;

    /// <summary>Resets the leave guard when the page is shown.</summary>
    public override void OnNavigatedTo() => _leaveGuard.Reset();

    /// <summary>Goes back without saving, asking first when something has been typed.</summary>
    [RelayCommand]
    private Task CancelAsync(CancellationToken cancellationToken = default) =>
        _leaveGuard.CancelAsync(HasUnsavedChanges, navigation.GoBack, cancellationToken);

    /// <summary>Asks before sidebar navigation or the back button leaves the page with typed input.</summary>
    public override Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default) =>
        _leaveGuard.CanLeaveAsync(HasUnsavedChanges, cancellationToken);

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Name.Trim().Length > MaxNameLength)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, "The subject name can be at most {0} characters.", MaxNameLength));
            return;
        }

        if (ShortCode.Trim().Length > MaxShortCodeLength)
        {
            ShowError(string.Format(CultureInfo.CurrentCulture, "The short code can be at most {0} characters.", MaxShortCodeLength));
            return;
        }

        try
        {
            await subjectService.CreateSubjectAsync(Name, ShortCode, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(
                "Error",
                string.Format(
                    CultureInfo.CurrentCulture,
                    "The subject could not be saved. Enter a name of up to {0} characters and a short code of up to {1} characters.",
                    SubjectService.MaxNameLength,
                    SubjectService.MaxShortCodeLength),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show("Success", "Subject has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        _leaveGuard.LeaveWithoutPrompt(navigation.GoBack);
    }

    private void ShowError(string message) =>
        snackbar.Show("Error", message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
}
