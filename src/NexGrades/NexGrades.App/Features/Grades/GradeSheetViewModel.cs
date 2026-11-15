using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Core.YearLock;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NexGrades.App.Features.Grades;

/// <summary>
/// The grade sheet of one subject instance: students as rows, assessments as columns grouped by bucket, live
/// bucket and subject averages. Grades are typed as tokens and each cell saves by itself when its edit ends. A
/// closed year is read-only. The business rules live in the Domain services; this view model only maps what the
/// user does to them and reports the outcome with fixed, translatable text.
/// </summary>
/// <param name="navigation">Navigates back from the empty state.</param>
/// <param name="snackbar">Reports the outcome of each action.</param>
/// <param name="gradeSheetService">Loads the sheet.</param>
/// <param name="gradeService">Records, updates, voids and restores grades.</param>
/// <param name="assessmentService">Creates, edits, voids and restores assessments.</param>
/// <param name="contentDialogService">Shows the confirmations for discarding input and for voiding an assessment.</param>
/// <param name="yearLockFactory">Creates the lock state of the sheet's school year, which gates every write.</param>
/// <param name="logger">Logs failures without any pupil data.</param>
public sealed partial class GradeSheetViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    GradeSheetService gradeSheetService,
    GradeService gradeService,
    AssessmentService assessmentService,
    IContentDialogService contentDialogService,
    YearLockViewModelFactory yearLockFactory,
    ILogger<GradeSheetViewModel> logger) : ViewModel
{
    private readonly ILogger<GradeSheetViewModel> _logger = logger;

    private int? _subjectInstanceId;
    private int _loadVersion;
    private int _busyCount;
    private bool _suppressVoidedReload;
    private IReadOnlyList<GradeSheetBucket> _sheetBuckets = [];

    // Set just before the page leaves by itself, so the leave guard does not ask a second time.
    private bool _allowLeave;

    /// <summary>
    /// The lock state of the sheet's school year. Every write needs <see cref="YearLock"/>'s
    /// <see cref="YearLockViewModel.IsWritable"/>; the page shows its unlock and lock-again controls.
    /// </summary>
    public YearLockViewModel YearLock { get; } = yearLockFactory.Create();

    /// <summary>The rows, one per student; active students first, then students who left.</summary>
    [ObservableProperty]
    private ObservableCollection<GradeSheetRow> _rows = [];

    /// <summary>The grid columns; bind them to <see cref="DataGridColumnsBehavior"/>.</summary>
    [ObservableProperty]
    private ObservableCollection<GradeSheetColumn> _columns = [];

    /// <summary>The sheet's buckets, for choosing where a new assessment goes.</summary>
    [ObservableProperty]
    private IReadOnlyList<GradeSheetBucket> _buckets = [];

    /// <summary>True when a sheet is loaded; false shows the empty state with a Back action.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    [NotifyPropertyChangedFor(nameof(IsWritable))]
    private bool _hasSheet;

    /// <summary>True while the sheet is loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>The whole-sentence explanation shown in the empty state.</summary>
    [ObservableProperty]
    private string _emptyStateMessage = string.Empty;

    /// <summary>The subject's name.</summary>
    [ObservableProperty]
    private string _subjectName = string.Empty;

    /// <summary>The class's name.</summary>
    [ObservableProperty]
    private string _className = string.Empty;

    /// <summary>The school year's label, for example "2025/26".</summary>
    [ObservableProperty]
    private string _schoolYearLabel = string.Empty;

    /// <summary>The page title: subject and class.</summary>
    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>True while any write (command or cell save) runs; several can overlap, so it is counted.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>When on, the sheet also shows voided grades and assessments, which can then be restored.</summary>
    [ObservableProperty]
    private bool _showVoidedGrades;

    /// <summary>
    /// The grade cell the grid is on; set it from <see cref="DataGridColumnsBehavior.SelectedCellProperty"/>. The
    /// detail panel edits its grade. Selecting another cell discards unsaved detail edits.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    private GradeCellViewModel? _selectedCell;

    /// <summary>The detail panel's date field, the date the grade was recorded for.</summary>
    [ObservableProperty]
    private DateTime? _detailDate;

    /// <summary>The detail panel's label field (at most <see cref="GradeService.MaxLabelLength"/> characters).</summary>
    [ObservableProperty]
    private string _detailLabel = string.Empty;

    /// <summary>The detail panel's comment field (at most <see cref="GradeService.MaxCommentLength"/> characters).</summary>
    [ObservableProperty]
    private string _detailComment = string.Empty;

    /// <summary>The bucket the new assessment goes into.</summary>
    [ObservableProperty]
    private GradeSheetBucket? _newAssessmentBucket;

    /// <summary>The new assessment's title (at most <see cref="AssessmentService.MaxTitleLength"/> characters).</summary>
    [ObservableProperty]
    private string _newAssessmentTitle = string.Empty;

    /// <summary>The new assessment's date; it must lie in the school year.</summary>
    [ObservableProperty]
    private DateTime? _newAssessmentDate = DateTime.Today;

    /// <summary>The assessment column being edited, or null when no edit is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingAssessment))]
    private GradeSheetColumn? _editingAssessment;

    /// <summary>The edit form's title.</summary>
    [ObservableProperty]
    private string _editAssessmentTitle = string.Empty;

    /// <summary>The edit form's date.</summary>
    [ObservableProperty]
    private DateTime? _editAssessmentDate;

    /// <summary>True when no sheet is shown: the page was opened without a known subject instance.</summary>
    public bool IsEmptyState => !HasSheet;

    /// <summary>True when the sheet accepts writes right now: it is loaded and its year is writable.</summary>
    public bool IsWritable => HasSheet && YearLock.IsWritable;

    /// <summary>True when the detail panel has an active grade to show.</summary>
    public bool HasDetail => SelectedCell is { HasActiveGrade: true };

    /// <summary>True when the detail panel differs from the saved grade.</summary>
    public bool IsDetailDirty =>
        SelectedCell is { HasActiveGrade: true, Grade: { } grade }
        && (DetailDate?.Date != grade.RecordedOn.ToDateTime(TimeOnly.MinValue)
            || DetailLabel.Trim() != (grade.Label ?? string.Empty)
            || DetailComment.Trim() != (grade.Comment ?? string.Empty));

    /// <summary>True when the assessment edit form is open.</summary>
    public bool IsEditingAssessment => EditingAssessment is not null;

    /// <summary>True when a cell holds text that is not saved, or the detail panel has unsaved edits.</summary>
    public bool HasUnsavedInput =>
        IsDetailDirty || Rows.Any(r => r.Cells.Any(c => c.HasError || c.HasUnsavedText) || r.FinalGrade.HasError || r.FinalGrade.HasUnsavedText);

    /// <summary>
    /// Loads the sheet for the subject instance id passed as <paramref name="parameter"/>. Without a known id the
    /// page shows its empty state.
    /// </summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        _allowLeave = false;
        _subjectInstanceId = parameter as int?;

        // Subscribed here rather than in a constructor; removing first keeps repeated visits from stacking handlers.
        YearLock.WritabilityChanged -= OnYearLockWritabilityChanged;
        YearLock.WritabilityChanged += OnYearLockWritabilityChanged;

        _suppressVoidedReload = true;
        ShowVoidedGrades = false;
        _suppressVoidedReload = false;

        ResetForms();

        if (_subjectInstanceId is null)
        {
            ClearSheet("This grade sheet could not be found.");
            return;
        }

        await LoadAsync();
    }

    /// <summary>Asks before sidebar navigation or the back button leaves the page with unsaved input.</summary>
    public override async Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default)
    {
        if (_allowLeave || !HasUnsavedInput)
        {
            return true;
        }

        return await ConfirmAsync(
            "Discard changes",
            "You have grades or details that are not saved. Leave the page and discard them?",
            "Discard",
            "Keep editing",
            cancellationToken);
    }

    /// <summary>Goes back, asking first when there is unsaved input.</summary>
    [RelayCommand]
    private async Task BackAsync(CancellationToken cancellationToken = default)
    {
        if (!await CanLeaveAsync(cancellationToken))
        {
            return;
        }

        _allowLeave = true;
        if (!navigation.GoBack())
        {
            _allowLeave = false;
        }
    }

    /// <summary>Saves the detail panel (date, label, comment) of the selected cell's grade.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveDetail))]
    private async Task SaveDetailAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCell is not { Grade: { } grade } cell || DetailDate is not { } date)
        {
            return;
        }

        var saved = await RunAsync(
            async () =>
            {
                var updated = await gradeService.UpdateGradeAsync(
                    grade.Id, grade.Value, DateOnly.FromDateTime(date), DetailLabel, DetailComment, cancellationToken);
                cell.ApplyGrade(ToSheetGrade(updated));
            },
            "The grade details could not be saved. Check that the date lies in the school year and that the student was enrolled and not exempt on that date.",
            "The grade details could not be saved because the school year is closed.");

        if (saved)
        {
            RefreshDetail();
            ShowSuccess("The grade details have been saved.");
        }
    }

    /// <summary>Throws away unsaved detail edits and shows the saved values again.</summary>
    [RelayCommand]
    private void DiscardDetail() => RefreshDetail();

    /// <summary>Voids the grade of the selected cell, so the detail panel can offer it next to the cell's own "clear to void".</summary>
    [RelayCommand(CanExecute = nameof(CanVoidGrade))]
    private async Task VoidGradeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCell is not { HasActiveGrade: true, Grade: { } grade } cell)
        {
            return;
        }

        // With "show voided" on the sheet reloads afterwards, which would discard typed input, so ask first.
        if (ShowVoidedGrades && !await ConfirmDiscardForReloadAsync(cancellationToken))
        {
            return;
        }

        var voided = await RunAsync(
            async () =>
            {
                await gradeService.VoidGradeAsync(grade.Id, cancellationToken);
                if (!ShowVoidedGrades)
                {
                    cell.ApplyGrade(null);
                    FindRow(cell)?.RecomputeAverages();
                }
            },
            "The grade could not be voided.",
            "The grade could not be voided because the school year is closed.");

        if (voided)
        {
            RefreshDetail();
            ShowSuccess("The grade has been voided.");

            // With "show voided" on, the voided grade stays in its cell, so the sheet reloads to show it.
            if (ShowVoidedGrades)
            {
                await ReloadAfterWriteAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Restores a voided grade. Without a parameter it acts on the selected cell, so the detail panel can offer it.
    /// </summary>
    /// <param name="cell">The cell with the voided grade, or null for the selected cell.</param>
    /// <param name="cancellationToken">Cancels the restore.</param>
    [RelayCommand(CanExecute = nameof(CanRestoreGrade))]
    private async Task RestoreGradeAsync(GradeCellViewModel? cell, CancellationToken cancellationToken = default)
    {
        cell ??= SelectedCell;
        if (cell is not { IsVoidedGrade: true, Grade: { } grade })
        {
            return;
        }

        var restored = await RunAsync(
            async () =>
            {
                await gradeService.RestoreGradeAsync(grade.Id, cancellationToken);
                cell.ApplyGrade(grade with { VoidedOn = null });
                FindRow(cell)?.RecomputeAverages();
            },
            "The grade could not be restored. Check that the student has no other grade for this assessment and was enrolled on its date.",
            "The grade could not be restored because the school year is closed.");

        if (restored)
        {
            RefreshDetail();
            ShowSuccess("The grade has been restored.");
        }
    }

    /// <summary>Adds an assessment to the chosen bucket and reloads the sheet to show its column.</summary>
    [RelayCommand(CanExecute = nameof(CanAddAssessment))]
    private async Task AddAssessmentAsync(CancellationToken cancellationToken = default)
    {
        if (NewAssessmentBucket is not { } bucket || NewAssessmentDate is not { } date)
        {
            return;
        }

        if (!await ConfirmDiscardForReloadAsync(cancellationToken))
        {
            return;
        }

        var added = await RunAsync(
            () => assessmentService.CreateAssessmentAsync(bucket.Id, NewAssessmentTitle, DateOnly.FromDateTime(date), cancellationToken),
            "The assessment could not be added. Enter a title and a date within the school year.",
            "The assessment could not be added because the school year is closed.");

        if (added)
        {
            NewAssessmentTitle = string.Empty;
            ShowSuccess("The assessment has been added.");
            await ReloadAfterWriteAsync(cancellationToken);
        }
    }

    /// <summary>Opens the edit form for an assessment column, prefilled with its title and date.</summary>
    /// <param name="column">The assessment column.</param>
    [RelayCommand(CanExecute = nameof(CanBeginEditAssessment))]
    private void BeginEditAssessment(GradeSheetColumn? column)
    {
        if (column is not { AssessmentId: not null, IsVoided: false })
        {
            return;
        }

        EditingAssessment = column;
        EditAssessmentTitle = column.AssessmentTitle;
        EditAssessmentDate = column.AssessmentDate?.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>Closes the edit form without saving.</summary>
    [RelayCommand]
    private void CancelEditAssessment() => EditingAssessment = null;

    /// <summary>Saves the edit form's title and date; grades still on the old date move with it.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveAssessmentEdit))]
    private async Task SaveAssessmentEditAsync(CancellationToken cancellationToken = default)
    {
        if (EditingAssessment is not { AssessmentId: { } id } || EditAssessmentDate is not { } date)
        {
            return;
        }

        if (!await ConfirmDiscardForReloadAsync(cancellationToken))
        {
            return;
        }

        var saved = await RunAsync(
            () => assessmentService.EditAssessmentAsync(id, EditAssessmentTitle, DateOnly.FromDateTime(date), cancellationToken),
            "The assessment could not be changed. Enter a title and a date within the school year, check that every graded student was enrolled and not exempt on the new date, and that the assessment is not voided.",
            "The assessment could not be changed because the school year is closed.");

        if (saved)
        {
            EditingAssessment = null;
            ShowSuccess("The assessment has been changed.");
            await ReloadAfterWriteAsync(cancellationToken);
        }
    }

    /// <summary>Voids an assessment after confirmation; its grades stay and come back with it when restored.</summary>
    /// <param name="column">The assessment column.</param>
    /// <param name="cancellationToken">Cancels the void.</param>
    [RelayCommand(CanExecute = nameof(CanVoidAssessment))]
    private async Task VoidAssessmentAsync(GradeSheetColumn? column, CancellationToken cancellationToken = default)
    {
        if (column is not { AssessmentId: { } id })
        {
            return;
        }

        // One dialog: voiding reloads the sheet, so unsaved input is part of the same question instead of a second one.
        var confirmed = await ConfirmAsync(
            "Void assessment",
            HasUnsavedInput
                ? "The grades of this assessment no longer count towards any average while it is voided. You can restore it later. Voiding reloads the sheet and discards the grades or details that are not saved. Void the assessment?"
                : "The grades of this assessment no longer count towards any average while it is voided. You can restore it later. Void the assessment?",
            "Void",
            "Cancel",
            cancellationToken);
        if (!confirmed)
        {
            return;
        }

        var voided = await RunAsync(
            () => assessmentService.VoidAssessmentAsync(id, cancellationToken),
            "The assessment could not be voided.",
            "The assessment could not be voided because the school year is closed.");

        if (voided)
        {
            ShowSuccess("The assessment has been voided.");
            await ReloadAfterWriteAsync(cancellationToken);
        }
    }

    /// <summary>Restores a voided assessment together with its grades.</summary>
    /// <param name="column">The voided assessment column.</param>
    /// <param name="cancellationToken">Cancels the restore.</param>
    [RelayCommand(CanExecute = nameof(CanRestoreAssessment))]
    private async Task RestoreAssessmentAsync(GradeSheetColumn? column, CancellationToken cancellationToken = default)
    {
        if (column is not { AssessmentId: { } id })
        {
            return;
        }

        if (!await ConfirmDiscardForReloadAsync(cancellationToken))
        {
            return;
        }

        var restored = await RunAsync(
            () => assessmentService.RestoreAssessmentAsync(id, cancellationToken),
            "The assessment could not be restored.",
            "The assessment could not be restored because the school year is closed.");

        if (restored)
        {
            ShowSuccess("The assessment has been restored.");
            await ReloadAfterWriteAsync(cancellationToken);
        }
    }

    private bool CanSaveDetail() =>
        IsWritable && !IsBusy && IsDetailDirty && DetailDate is not null && SelectedCell is { HasError: false };

    private bool CanBeginEditAssessment(GradeSheetColumn? column) =>
        IsWritable && column is { AssessmentId: not null, IsVoided: false };

    private bool CanVoidGrade() => IsWritable && !IsBusy && SelectedCell is { HasActiveGrade: true };

    private bool CanRestoreGrade(GradeCellViewModel? cell) =>
        IsWritable && !IsBusy && (cell ?? SelectedCell) is { IsVoidedGrade: true };

    private bool CanAddAssessment() =>
        IsWritable && !IsBusy && NewAssessmentBucket is not null && NewAssessmentDate is not null
        && !string.IsNullOrWhiteSpace(NewAssessmentTitle);

    private bool CanSaveAssessmentEdit() =>
        IsWritable && !IsBusy && EditingAssessment is not null && EditAssessmentDate is not null
        && !string.IsNullOrWhiteSpace(EditAssessmentTitle);

    private bool CanVoidAssessment(GradeSheetColumn? column) =>
        IsWritable && !IsBusy && column is { AssessmentId: not null, IsVoided: false };

    private bool CanRestoreAssessment(GradeSheetColumn? column) =>
        IsWritable && !IsBusy && column is { AssessmentId: not null, IsVoided: true };

    partial void OnIsBusyChanged(bool value) => NotifyWriteCommands();

    partial void OnShowVoidedGradesChanged(bool value)
    {
        if (!_suppressVoidedReload && _subjectInstanceId is not null)
        {
            _ = ReloadForVoidedToggleAsync(value);
        }
    }

    /// <summary>
    /// Reloads the sheet after the user switched "Show voided grades" to <paramref name="shown"/>. The reload replaces
    /// every row, so with unsaved input the user is asked first; declining keeps the sheet and puts the toggle back
    /// without reloading.
    /// </summary>
    /// <param name="shown">The value the toggle was just switched to.</param>
    internal async Task ReloadForVoidedToggleAsync(bool shown)
    {
        try
        {
            if (await ConfirmDiscardForReloadAsync())
            {
                await LoadAsync();
                return;
            }

            // A newer toggle already changed the value again; then there is nothing to revert.
            if (ShowVoidedGrades == shown)
            {
                _suppressVoidedReload = true;
                try
                {
                    ShowVoidedGrades = !shown;
                }
                finally
                {
                    _suppressVoidedReload = false;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fire-and-forget caller: nothing above can observe this, so report it here.
            LogVoidedToggleReloadFailed(ex);
            ShowError("The grade sheet could not be reloaded. It may be out of date until you reload it.");
        }
    }

    /// <summary>Asks whether a reload may discard unsaved input; true without a question when there is none.</summary>
    private async Task<bool> ConfirmDiscardForReloadAsync(CancellationToken cancellationToken = default)
    {
        if (!HasUnsavedInput)
        {
            return true;
        }

        return await ConfirmAsync(
            "Discard changes",
            "You have grades or details that are not saved. Reloading the sheet discards them. Continue?",
            "Discard",
            "Keep editing",
            cancellationToken);
    }

    /// <summary>
    /// Reloads after a write succeeded. Input typed while the write ran is asked about again; declining skips the
    /// reload, so that input is never cleared.
    /// </summary>
    private async Task ReloadAfterWriteAsync(CancellationToken cancellationToken)
    {
        if (await ConfirmDiscardForReloadAsync(cancellationToken))
        {
            await LoadAsync();
            return;
        }

        ShowError("The sheet may be out of date until you reload it.");
    }

    partial void OnSelectedCellChanged(GradeCellViewModel? value) => RefreshDetail();

    partial void OnDetailDateChanged(DateTime? value) => OnDetailEdited();

    partial void OnDetailLabelChanged(string value) => OnDetailEdited();

    partial void OnDetailCommentChanged(string value) => OnDetailEdited();

    partial void OnNewAssessmentBucketChanged(GradeSheetBucket? value) => AddAssessmentCommand.NotifyCanExecuteChanged();

    partial void OnNewAssessmentTitleChanged(string value) => AddAssessmentCommand.NotifyCanExecuteChanged();

    partial void OnNewAssessmentDateChanged(DateTime? value) => AddAssessmentCommand.NotifyCanExecuteChanged();

    partial void OnEditingAssessmentChanged(GradeSheetColumn? value) => SaveAssessmentEditCommand.NotifyCanExecuteChanged();

    partial void OnEditAssessmentTitleChanged(string value) => SaveAssessmentEditCommand.NotifyCanExecuteChanged();

    partial void OnEditAssessmentDateChanged(DateTime? value) => SaveAssessmentEditCommand.NotifyCanExecuteChanged();

    private void OnDetailEdited()
    {
        OnPropertyChanged(nameof(IsDetailDirty));
        SaveDetailCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows the saved grade details of the selected cell in the detail fields.</summary>
    private void RefreshDetail()
    {
        var grade = SelectedCell is { HasActiveGrade: true } ? SelectedCell.Grade : null;
        DetailDate = grade?.RecordedOn.ToDateTime(TimeOnly.MinValue);
        DetailLabel = grade?.Label ?? string.Empty;
        DetailComment = grade?.Comment ?? string.Empty;
        OnPropertyChanged(nameof(HasDetail));
        OnDetailEdited();
        RestoreGradeCommand.NotifyCanExecuteChanged();
        VoidGradeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Re-evaluates everything that depends on whether the sheet's year is writable; typed text stays.</summary>
    private void OnYearLockWritabilityChanged(object? sender, EventArgs e) => ApplyWritability();

    private void ApplyWritability()
    {
        foreach (var row in Rows)
        {
            row.SetWritable(YearLock.IsWritable);
        }

        OnPropertyChanged(nameof(IsWritable));
        NotifyWriteCommands();
    }

    private void NotifyWriteCommands()
    {
        SaveDetailCommand.NotifyCanExecuteChanged();
        RestoreGradeCommand.NotifyCanExecuteChanged();
        VoidGradeCommand.NotifyCanExecuteChanged();
        BeginEditAssessmentCommand.NotifyCanExecuteChanged();
        AddAssessmentCommand.NotifyCanExecuteChanged();
        SaveAssessmentEditCommand.NotifyCanExecuteChanged();
        VoidAssessmentCommand.NotifyCanExecuteChanged();
        RestoreAssessmentCommand.NotifyCanExecuteChanged();
    }

    private void ResetForms()
    {
        SelectedCell = null;
        EditingAssessment = null;
        NewAssessmentBucket = null;
        NewAssessmentTitle = string.Empty;
        NewAssessmentDate = DateTime.Today;
    }

    private void ClearSheet(string emptyStateMessage)
    {
        Rows = [];
        Columns = [];
        Buckets = [];
        _sheetBuckets = [];
        SelectedCell = null;
        SubjectName = string.Empty;
        ClassName = string.Empty;
        SchoolYearLabel = string.Empty;
        Title = string.Empty;
        EmptyStateMessage = emptyStateMessage;
        HasSheet = false;
        NotifyWriteCommands();
    }

    /// <summary>True when the detail panel or any cell other than <paramref name="cell"/> holds unsaved input.</summary>
    private bool HasUnsavedInputExcept(GradeCellViewModel cell) =>
        IsDetailDirty
        || Rows.Any(r => r.Cells.Any(c => !ReferenceEquals(c, cell) && (c.HasError || c.HasUnsavedText))
            || r.FinalGrade.HasError || r.FinalGrade.HasUnsavedText);

    /// <summary>
    /// Loads the sheet and replaces rows and columns. Loads can overlap (toggling "show voided" quickly), so only
    /// the newest may write its result. A failure is reported rather than thrown, because this also runs
    /// fire-and-forget from property-change handlers.
    /// </summary>
    private async Task LoadAsync()
    {
        if (_subjectInstanceId is not { } id)
        {
            return;
        }

        var version = ++_loadVersion;
        IsLoading = true;
        try
        {
            GradeSheet sheet;
            try
            {
                sheet = await gradeSheetService.GetGradeSheetAsync(id, ShowVoidedGrades);
            }
            catch (InvariantViolationException)
            {
                if (version == _loadVersion)
                {
                    ClearSheet("This grade sheet could not be found.");
                }

                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogLoadFailed(ex);
                if (version == _loadVersion)
                {
                    ClearSheet("The grade sheet could not be loaded.");
                    ShowError("The grade sheet could not be loaded.");
                }

                return;
            }

            if (version != _loadVersion)
            {
                return;
            }

            ApplySheet(sheet);

            await YearLock.ShowAsync(sheet.SchoolYearId);
            ApplyWritability();
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>Shows a loaded sheet: header, columns and rows.</summary>
    private void ApplySheet(GradeSheet sheet)
    {
        _sheetBuckets = sheet.Buckets;
        Buckets = sheet.Buckets;
        SubjectName = sheet.SubjectName;
        ClassName = sheet.ClassName;
        SchoolYearLabel = sheet.SchoolYearLabel;
        Title = string.Format(CultureInfo.CurrentCulture, "{0}, {1}", sheet.SubjectName, sheet.ClassName);
        EmptyStateMessage = string.Empty;

        // Rows first, so no column ever points at cells that are gone.
        SelectedCell = null;
        Rows = [];
        Columns = [.. GradeSheetColumnBuilder.Build(sheet)];
        Rows = [.. sheet.Students.Select(s => new GradeSheetRow(s, _sheetBuckets, sheet.IsWritable, OnCellTextCommitted, OnFinalCellTextCommitted))];
        HasSheet = true;

        NewAssessmentBucket = sheet.Buckets.FirstOrDefault(b => NewAssessmentBucket is null || b.Id == NewAssessmentBucket.Id);
    }

    private void OnCellTextCommitted(GradeCellViewModel cell) => _ = SaveSerializedAsync(cell, () => SaveCellAsync(cell));

    private void OnFinalCellTextCommitted(FinalGradeCellViewModel cell) => _ = SaveSerializedAsync(cell, () => SaveFinalCellAsync(cell));

    /// <summary>
    /// Saves one cell (assessment or final grade) at a time. A commit that arrives while a save of the same cell runs is queued, and the
    /// latest text is saved after the running call completes, so quick re-entry never records a grade twice. A
    /// finished save leaves newer text in the cell alone (see <see cref="GradeCellViewModel.ApplyGrade(GradeSheetGrade?, string?)"/>),
    /// so the queued pass compares it with the freshly saved grade. Runs fire-and-forget, so nothing escapes it: an unexpected failure is logged, reported
    /// with a generic message and marked on the cell.
    /// </summary>
    private async Task SaveSerializedAsync(IGradeSheetEditableCell cell, Func<Task> save)
    {
        if (cell.IsSaving)
        {
            cell.SaveQueued = true;
            return;
        }

        cell.IsSaving = true;
        try
        {
            do
            {
                cell.SaveQueued = false;
                try
                {
                    await save();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogCellSaveFailed(ex);
                    cell.HasError = true;
                    ShowError("The grade could not be saved because of an unexpected problem.");
                }
            }
            while (cell.SaveQueued);
        }
        finally
        {
            cell.IsSaving = false;
        }
    }

    /// <summary>
    /// Saves one cell after its edit ended. Empty to empty records nothing; a new token records a grade, a
    /// changed one updates it keeping date, label and comment, and an emptied cell voids it. A token that is not
    /// on the scale is never sent; the cell keeps its text and the error mark.
    /// </summary>
    private async Task SaveCellAsync(GradeCellViewModel cell)
    {
        var text = cell.Text.Trim();

        if (text.Length == 0)
        {
            if (cell.Grade is not { VoidedOn: null } voidable || cell.IsAssessmentVoided)
            {
                cell.SetTextWithoutCommit(string.Empty);
                cell.HasError = false;
                return;
            }

            await VoidCellAsync(cell, voidable);
            return;
        }

        if (!GradingScale.TryParseToken(text, out var value))
        {
            cell.HasError = true;
            return;
        }

        var token = GradingScale.TokenForValue(value);
        if (cell.HasActiveGrade && cell.Grade!.Token == token)
        {
            cell.SetTextWithoutCommit(token);
            cell.HasError = false;
            return;
        }

        await SaveCellChangeAsync(
            cell,
            async () =>
            {
                var saved = cell.Grade is { VoidedOn: null } existing && cell.HasActiveGrade
                    ? await gradeService.UpdateGradeAsync(existing.Id, value, existing.RecordedOn, existing.Label, existing.Comment)
                    : await gradeService.RecordAssessmentGradeAsync(cell.AssessmentId, cell.StudentId, value);
                cell.ApplyGrade(ToSheetGrade(saved), text);
            },
            "The grade could not be saved. Check that the student was enrolled on the assessment's date and is not exempt.",
            "The grade could not be saved because its school year is closed.",
            successMessage: null,
            reloadAfter: () => false);
    }

    /// <summary>
    /// Saves the final grade cell after its edit ended. Empty clears a stored override (and only normalises when
    /// there is none); a legal whole grade sets the override unless it equals the stored one; anything else is never
    /// sent, and the cell keeps its text and the error mark. A closed year keeps the text and marks the error.
    /// </summary>
    private async Task SaveFinalCellAsync(FinalGradeCellViewModel cell)
    {
        if (_subjectInstanceId is not { } subjectInstanceId)
        {
            return;
        }

        var text = cell.Text.Trim();

        if (text.Length == 0)
        {
            await ClearFinalCellAsync(subjectInstanceId, cell, text);
            return;
        }

        if (!GradingScale.TryParseFinalGradeToken(text, out var value))
        {
            cell.HasError = true;
            return;
        }

        if (cell.Override == value)
        {
            cell.SetTextWithoutCommit(FinalGradeCellViewModel.FormatGrade(value));
            cell.HasError = false;
            return;
        }

        var saved = await RunAsync(
            async () =>
            {
                await gradeService.SetFinalGradeOverrideAsync(subjectInstanceId, cell.StudentId, value);
                cell.ApplyOverride(value, text);
            },
            FinalGradeFailureMessage,
            FinalGradeClosedYearMessage);
        if (!saved)
        {
            cell.HasError = true;
        }
    }

    private async Task ClearFinalCellAsync(int subjectInstanceId, FinalGradeCellViewModel cell, string text)
    {
        if (cell.Override is null)
        {
            cell.SetTextWithoutCommit(string.Empty);
            cell.HasError = false;
            return;
        }

        var cleared = await RunAsync(
            async () =>
            {
                await gradeService.ClearFinalGradeOverrideAsync(subjectInstanceId, cell.StudentId);
                cell.ApplyOverride(null, text);
            },
            FinalGradeFailureMessage,
            FinalGradeClosedYearMessage);
        if (cleared)
        {
            ShowSuccess("The override has been removed. The proposal is used again.");
        }
        else
        {
            cell.HasError = true;
        }
    }

    private const string FinalGradeFailureMessage =
        "The final grade could not be saved. Enter a whole grade from 1 to 6 for a student who is enrolled and not exempt.";

    private const string FinalGradeClosedYearMessage = "The final grade could not be saved because the school year is closed.";

    /// <summary>
    /// Voids the grade of a cell the user emptied. With "show voided" on, the sheet reloads so the voided grade
    /// shows in its cell and can be restored. If the user typed a new value meanwhile, the cell drops the voided
    /// grade without a reload (which would discard the typed text), and the queued save records the new value.
    /// </summary>
    private async Task VoidCellAsync(GradeCellViewModel cell, GradeSheetGrade grade)
    {
        var text = cell.Text.Trim();
        var keepSheet = false;
        await SaveCellChangeAsync(
            cell,
            async () =>
            {
                await gradeService.VoidGradeAsync(grade.Id);

                // A queued save counts as typed meanwhile even when its text equals the emptied one: the queued pass
                // must see the cell without the voided grade, or it would void the same grade a second time.
                // Unsaved input elsewhere also keeps the sheet, because a reload would discard it.
                var typedMeanwhile = cell.SaveQueued || cell.Text.Trim() != text;
                keepSheet = typedMeanwhile || HasUnsavedInputExcept(cell);
                if (!ShowVoidedGrades || keepSheet)
                {
                    cell.ApplyGrade(null, text);
                }
            },
            "The grade could not be voided.",
            "The grade could not be voided because the school year is closed.",
            "The grade has been voided.",
            reloadAfter: () => ShowVoidedGrades && !keepSheet && !HasUnsavedInputExcept(cell));
    }

    /// <summary>Runs one cell's write and, on success, recomputes that row's averages; on failure marks the cell.</summary>
    private async Task SaveCellChangeAsync(
        GradeCellViewModel cell,
        Func<Task> write,
        string failureMessage,
        string closedYearMessage,
        string? successMessage,
        Func<bool> reloadAfter)
    {
        var saved = await RunAsync(write, failureMessage, closedYearMessage);
        if (!saved)
        {
            cell.HasError = true;
            return;
        }

        FindRow(cell)?.RecomputeAverages();
        if (SelectedCell == cell)
        {
            RefreshDetail();
        }

        if (successMessage is not null)
        {
            ShowSuccess(successMessage);
        }

        if (reloadAfter())
        {
            await LoadAsync();
        }
    }

    /// <summary>
    /// Runs one write and reports a failure with fixed, translatable text; exception messages are diagnostic and
    /// never shown. A refusal because the year is closed only refreshes the lock state, so the page turns read-only
    /// instead of offering edits that fail again; the typed text stays and nothing is reloaded.
    /// <see cref="ClosedYearException"/> derives from <see cref="InvariantViolationException"/>, so it is caught
    /// first.
    /// </summary>
    private async Task<bool> RunAsync(Func<Task> operation, string failureMessage, string closedYearMessage)
    {
        _busyCount++;
        IsBusy = true;
        try
        {
            await operation();
            return true;
        }
        catch (ClosedYearException ex)
        {
            LogWriteRefused(ex);
            ShowError(closedYearMessage);
            await YearLock.RefreshAsync();
            return false;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            LogWriteFailed(ex);
            ShowError(failureMessage);
            return false;
        }
        finally
        {
            _busyCount--;
            IsBusy = _busyCount > 0;
        }
    }

    private GradeSheetRow? FindRow(GradeCellViewModel cell) => Rows.FirstOrDefault(r => r.StudentId == cell.StudentId);

    private static GradeSheetGrade ToSheetGrade(Grade grade) =>
        new(
            grade.Id,
            grade.GradeBucketId,
            grade.Value,
            GradingScale.TokenForValue(grade.Value),
            grade.RecordedOn,
            grade.Label,
            grade.Comment,
            grade.VoidedOn);

    private async Task<bool> ConfirmAsync(
        string title, string content, string primary, string close, CancellationToken cancellationToken)
    {
        var confirmation = await contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primary,
                CloseButtonText = close,
            },
            cancellationToken);

        return confirmation == ContentDialogResult.Primary;
    }

    private void ShowSuccess(string message) =>
        snackbar.Show("Success", message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));

    private void ShowError(string message) =>
        snackbar.Show("Error", message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

    [LoggerMessage(Level = LogLevel.Error, Message = "Loading the grade sheet failed.")]
    private partial void LogLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Saving a grade cell failed unexpectedly.")]
    private partial void LogCellSaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reloading the grade sheet after the voided-grades toggle failed.")]
    private partial void LogVoidedToggleReloadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A grade sheet change was refused because the school year is closed.")]
    private partial void LogWriteRefused(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A grade sheet change was rejected.")]
    private partial void LogWriteFailed(Exception exception);
}
