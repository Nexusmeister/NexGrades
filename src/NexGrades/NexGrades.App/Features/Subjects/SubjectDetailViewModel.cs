using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.App.Localization;
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

    [ObservableProperty] private string _title = TextResources.Text("Subjects_DetailPage_Add_Title");
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
            snackbar.Show(TextResources.Text("Common_Error"), TextResources.Text("Subjects_DetailPage_NotFound"), ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
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
        Title = TextResources.Text(subjectId is null ? "Subjects_DetailPage_Add_Title" : "Subjects_DetailPage_Edit_Title");
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
            ShowError(TextResources.Format("Subjects_DetailPage_Name_TooLong_Format", MaxNameLength));
            return;
        }

        if (ShortCode.Trim().Length > MaxShortCodeLength)
        {
            ShowError(TextResources.Format("Subjects_DetailPage_ShortCode_TooLong_Format", MaxShortCodeLength));
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
            ShowError(TextResources.Format(
                "Subjects_DetailPage_Save_Failed_Format", SubjectService.MaxNameLength, SubjectService.MaxShortCodeLength));
            return;
        }

        snackbar.Show(TextResources.Text("Common_Success"), TextResources.Text("Subjects_DetailPage_Saved"), ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        _leaveGuard.LeaveWithoutPrompt(navigation.GoBack);
    }

    private void ShowError(string message) =>
        snackbar.Show(TextResources.Text("Common_Error"), message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
}
