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

/// <summary>
/// Add and edit form for a subject. Editing changes the name and short code only; bucket templates are not touched.
/// </summary>
/// <param name="navigation">Navigates back after a save or a cancel.</param>
/// <param name="snackbar">Reports the outcome of a save.</param>
/// <param name="subjectService">Loads, validates and saves the subject.</param>
/// <param name="contentDialogService">Asks before unsaved input is discarded.</param>
public partial class SubjectDetailViewModel(INavigationService navigation, ISnackbarService snackbar, SubjectService subjectService, IContentDialogService contentDialogService) : ViewModel
{
    private readonly LeaveGuard _leaveGuard = new(contentDialogService);
    private int? _editingSubjectId;
    private string _loadedName = string.Empty;
    private string _loadedShortCode = string.Empty;

    [ObservableProperty] private string _title = "Add subject";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _shortCode = string.Empty;

    /// <summary>Longest subject name the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxNameLength { get; } = SubjectService.MaxNameLength;

    /// <summary>Longest short code the form accepts; for the text box's <c>MaxLength</c> and a counter.</summary>
    public int MaxShortCodeLength { get; } = SubjectService.MaxShortCodeLength;

    /// <summary>True when the form edits an existing subject rather than adding a new one.</summary>
    public bool IsEditing => _editingSubjectId is not null;

    /// <summary>True when the name or the short code differs from the loaded values (empty for a new subject).</summary>
    private bool HasUnsavedChanges =>
        !string.Equals(Name.Trim(), _loadedName, StringComparison.Ordinal)
        || !string.Equals(ShortCode.Trim(), _loadedShortCode, StringComparison.Ordinal);

    /// <summary>
    /// Loads the subject when the page opens for editing, with the subject id as <paramref name="parameter"/>.
    /// Otherwise resets the form to the add state.
    /// </summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        _leaveGuard.Reset();
        SetFormState(null, string.Empty, string.Empty);

        if (parameter is not int id)
        {
            return;
        }

        var subject = await subjectService.FindSubjectAsync(id);
        if (subject is null)
        {
            snackbar.Show("Error", "The subject could not be found. It may have been removed.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        SetFormState(subject.Id, subject.Name, subject.ShortCode);
    }

    /// <summary>Sets the mode and the loaded baseline together, so the leave guard compares against what was loaded.</summary>
    private void SetFormState(int? subjectId, string name, string shortCode)
    {
        _editingSubjectId = subjectId;
        _loadedName = name.Trim();
        _loadedShortCode = shortCode.Trim();
        Name = name;
        ShortCode = shortCode;
        Title = subjectId is null ? "Add subject" : "Edit subject";
        OnPropertyChanged(nameof(IsEditing));
    }

    /// <summary>Goes back without saving, asking first when something has been changed.</summary>
    [RelayCommand]
    private Task CancelAsync(CancellationToken cancellationToken = default) =>
        _leaveGuard.CancelAsync(HasUnsavedChanges, navigation.GoBack, cancellationToken);

    /// <summary>Asks before sidebar navigation or the back button leaves the page with changed input.</summary>
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
            if (_editingSubjectId is { } id)
            {
                await subjectService.UpdateSubjectAsync(id, Name, ShortCode, cancellationToken);
            }
            else
            {
                await subjectService.CreateSubjectAsync(Name, ShortCode, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            ShowError(string.Format(
                CultureInfo.CurrentCulture,
                "The subject could not be saved. Enter a name of up to {0} characters and a short code of up to {1} characters.",
                SubjectService.MaxNameLength,
                SubjectService.MaxShortCodeLength));
            return;
        }

        snackbar.Show("Success", "Subject has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        _leaveGuard.LeaveWithoutPrompt(navigation.GoBack);
    }

    private void ShowError(string message) =>
        snackbar.Show("Error", message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
}
