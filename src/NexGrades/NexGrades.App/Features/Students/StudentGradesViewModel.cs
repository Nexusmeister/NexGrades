using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Core.YearLock;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Students;

/// <summary>
/// The grades tab of the student page: the student's grades in the shown school year, per subject and bucket, with
/// the averages, and one shared inline editor to add, edit, void and restore a single grade. It is not a page of
/// its own: <see cref="StudentViewModel"/> owns it, feeds it the selected year and shares its
/// <see cref="YearLockViewModel"/>, so a closed year is read-only here exactly as on the notes tab. The business
/// rules live in the Domain services; this view model only maps what the user does to them and reports the
/// outcome with fixed, translatable text.
/// </summary>
/// <param name="snackbar">Reports the outcome of each action.</param>
/// <param name="gradeSheetService">Loads the student's grades.</param>
/// <param name="gradeService">Records, updates, voids and restores grades.</param>
/// <param name="clock">Source of "today" for the default date and of the time a void shows.</param>
/// <param name="logger">Logs failures without any pupil data.</param>
public sealed partial class StudentGradesViewModel(
    ISnackbarService snackbar,
    GradeSheetService gradeSheetService,
    GradeService gradeService,
    IClock clock,
    ILogger<StudentGradesViewModel> logger) : ViewModel
{
    private const string NoAssessmentText = "No assessment (single grade)";

    private readonly ILogger<StudentGradesViewModel> _logger = logger;

    private YearLockViewModel? _yearLock;
    private int? _studentId;
    private int? _schoolYearId;
    private DateOnly? _joinedOn;
    private DateOnly? _leftOn;
    private DateOnly? _yearStart;
    private DateOnly? _yearEnd;
    private int _loadVersion;
    private bool _loadFailed;

    private int _editorBucketId;
    private int? _editingAssessmentId;
    private DateTime? _addDefaultDate;
    private DateTime? _manualDate;
    private string _originalGradeText = string.Empty;
    private DateTime? _originalDate;
    private string _originalLabel = string.Empty;
    private string _originalComment = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoSubjects), nameof(EmptyMessage))]
    private ObservableCollection<StudentSubjectRow> _subjects = [];

    /// <summary>Whether voided grades are listed too, so they can be restored.</summary>
    [ObservableProperty]
    private bool _showVoidedGrades;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private int? _editingGradeId;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorBucketName = string.Empty;

    /// <summary>The assessment shown for an assessment grade that is being edited; it cannot be changed.</summary>
    [ObservableProperty]
    private string? _editingAssessmentTitle;

    [ObservableProperty]
    private ObservableCollection<AssessmentChoice> _assessmentChoices = [];

    [ObservableProperty]
    private AssessmentChoice? _selectedAssessment;

    /// <summary>The typed grade token, such as "2-". Kept as typed when it is invalid.</summary>
    [ObservableProperty]
    private string _gradeText = string.Empty;

    [ObservableProperty]
    private DateTime? _gradeDate;

    [ObservableProperty]
    private string _gradeLabel = string.Empty;

    [ObservableProperty]
    private string _gradeComment = string.Empty;

    /// <summary>True when the student has no subject to show for the selected year.</summary>
    public bool HasNoSubjects => Subjects.Count == 0;

    /// <summary>Explains an empty list, depending on why it is empty.</summary>
    public string EmptyMessage =>
        _loadFailed
            ? "The grades could not be loaded."
            : _schoolYearId is null
                ? "This student is not enrolled in any school year yet, so there are no grades."
                : "This student has no subjects in this school year.";

    /// <summary>Whether grades may be added or changed in the shown year right now; follows the page's year lock.</summary>
    public bool IsWritable => _schoolYearId is not null && _yearLock is { IsWritable: true };

    /// <summary>True while the editor is for an existing grade rather than a new one.</summary>
    public bool IsEditingGrade => EditingGradeId is not null;

    /// <summary>True when the editor holds a grade for an assessment of the bucket, which fixes its date.</summary>
    public bool IsAssessmentPicked => SelectedAssessment?.Id is not null;

    /// <summary>True when the assessment picker is shown: a new grade in a bucket that has assessments to choose.</summary>
    public bool ShowAssessmentPicker => !IsEditingGrade && AssessmentChoices.Count > 1;

    /// <summary>True when the editor shows the title of the assessment of the grade being edited.</summary>
    public bool ShowEditingAssessment => EditingAssessmentTitle is not null;

    /// <summary>True when the label field is shown; an assessment grade has no label.</summary>
    public bool ShowLabelField => !IsAssessmentPicked && _editingAssessmentId is null;

    /// <summary>True when the date is fixed to the assessment's date and cannot be changed.</summary>
    public bool IsDateReadOnly => IsAssessmentPicked || _editingAssessmentId is not null;

    /// <summary>
    /// Why the input can't be saved yet, shown next to the editor; <see langword="null"/> when the input is valid
    /// or the grade is still empty. The typed text stays so it can be corrected.
    /// </summary>
    public string? GradeValidationMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(GradeText) && !GradingScale.TryParseToken(GradeText, out _))
            {
                return "Enter a grade from 1+ to 6, such as 2-.";
            }

            if (GradeDate is null)
            {
                return "Pick a date for the grade.";
            }

            if (GradeLabel.Trim().Length > GradeService.MaxLabelLength)
            {
                return string.Format(CultureInfo.CurrentCulture, "The label can have at most {0} characters.", GradeService.MaxLabelLength);
            }

            return GradeComment.Trim().Length > GradeService.MaxCommentLength
                ? string.Format(CultureInfo.CurrentCulture, "The comment can have at most {0} characters.", GradeService.MaxCommentLength)
                : null;
        }
    }

    /// <summary>True when <see cref="GradeValidationMessage"/> has something to show.</summary>
    public bool HasGradeValidationMessage => GradeValidationMessage is not null;

    /// <summary>
    /// True while the editor holds input that isn't saved: for a new grade any field the user filled in, for an
    /// existing grade any field that differs from what was loaded.
    /// </summary>
    public bool HasPendingInput
    {
        get
        {
            if (!IsEditorOpen)
            {
                return false;
            }

            if (IsEditingGrade)
            {
                return !SameGrade(GradeText, _originalGradeText)
                    || GradeDate?.Date != _originalDate?.Date
                    || GradeLabel.Trim() != _originalLabel
                    || GradeComment.Trim() != _originalComment;
            }

            return !string.IsNullOrWhiteSpace(GradeText)
                || !string.IsNullOrWhiteSpace(GradeLabel)
                || !string.IsNullOrWhiteSpace(GradeComment)
                || IsAssessmentPicked
                || GradeDate?.Date != _addDefaultDate?.Date;
        }
    }

    /// <summary>
    /// True when the editor is shown: it is open and the year is writable, or it still holds input the user typed
    /// before the year turned read-only (an unlock that expired), which must stay visible so it can be copied or
    /// discarded.
    /// </summary>
    public bool ShowEditor => IsEditorOpen && (IsWritable || HasPendingInput);

    /// <summary>
    /// Receives the page's year lock, once. Grades are writable exactly when the lock says so; there is no second
    /// lock. The page calls <see cref="OnWritabilityChanged"/> when the lock flips.
    /// </summary>
    /// <param name="yearLock">The lock state of the school year the page shows.</param>
    public void Attach(YearLockViewModel yearLock)
    {
        ArgumentNullException.ThrowIfNull(yearLock);
        _yearLock = yearLock;
    }

    /// <summary>
    /// Re-evaluates everything that depends on whether the shown year is writable. It never touches the editor's
    /// fields and never reloads, so a lock that flips while the user types loses nothing.
    /// </summary>
    public void OnWritabilityChanged()
    {
        OnPropertyChanged(nameof(IsWritable));
        OnPropertyChanged(nameof(ShowEditor));
        NotifyCommands();
    }

    /// <summary>
    /// Loads the student's grades for <paramref name="schoolYearId"/>. Loads can overlap (switching year or the
    /// voided filter quickly), so only the newest may write its result. The editor is never touched, so a reload
    /// keeps unsaved input. A failure is reported rather than thrown, because this also runs fire-and-forget.
    /// </summary>
    /// <param name="studentId">The student.</param>
    /// <param name="schoolYearId">The year to show, or <see langword="null"/> when the student has none.</param>
    public async Task LoadAsync(int studentId, int? schoolYearId)
    {
        _studentId = studentId;
        _schoolYearId = schoolYearId;
        OnPropertyChanged(nameof(IsWritable));
        OnPropertyChanged(nameof(ShowEditor));
        NotifyCommands();
        await ReloadAsync();
    }

    /// <summary>Throws away what the editor holds, whether a new grade or an edit, without asking.</summary>
    public void CancelInput()
    {
        IsEditorOpen = false;
        EditingGradeId = null;
        _editingAssessmentId = null;
        EditingAssessmentTitle = null;
        _editorBucketId = 0;
        GradeText = string.Empty;
        GradeLabel = string.Empty;
        GradeComment = string.Empty;
        GradeDate = null;
        SelectedAssessment = null;
        AssessmentChoices = [];
        _addDefaultDate = null;
        _manualDate = null;
        _originalGradeText = string.Empty;
        _originalDate = null;
        _originalLabel = string.Empty;
        _originalComment = string.Empty;
        NotifyInputChanged();
    }

    partial void OnShowVoidedGradesChanged(bool value) => _ = ReloadAsync();

    partial void OnGradeTextChanged(string value) => NotifyInputChanged();

    partial void OnGradeDateChanged(DateTime? value) => NotifyInputChanged();

    partial void OnGradeLabelChanged(string value) => NotifyInputChanged();

    partial void OnGradeCommentChanged(string value) => NotifyInputChanged();

    partial void OnIsBusyChanged(bool value) => NotifyCommands();

    partial void OnIsEditorOpenChanged(bool value) => NotifyInputChanged();

    partial void OnEditingGradeIdChanged(int? value) => NotifyInputChanged();

    partial void OnEditingAssessmentTitleChanged(string? value) => NotifyInputChanged();

    partial void OnAssessmentChoicesChanged(ObservableCollection<AssessmentChoice> value) => NotifyInputChanged();

    partial void OnSelectedAssessmentChanged(AssessmentChoice? oldValue, AssessmentChoice? newValue)
    {
        var wasPicked = oldValue?.Id is not null;
        if (newValue is { Id: not null, Date: { } date })
        {
            // Remember the date the user had, so choosing "no assessment" again gives it back.
            if (!wasPicked)
            {
                _manualDate = GradeDate;
            }

            GradeDate = date.ToDateTime(TimeOnly.MinValue);
            GradeLabel = string.Empty;
        }
        else if (wasPicked)
        {
            GradeDate = _manualDate;
        }

        NotifyInputChanged();
    }

    private bool CanBeginAddGrade(StudentBucketRow? bucket) =>
        IsWritable && !IsBusy && !HasPendingInput && bucket is { IsExempt: false };

    /// <summary>Opens the editor for a new grade in <paramref name="bucket"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanBeginAddGrade))]
    private void BeginAddGrade(StudentBucketRow bucket)
    {
        CancelInput();

        _editorBucketId = bucket.BucketId;
        EditorBucketName = bucket.Name;
        EditorTitle = "New grade";

        var culture = CultureInfo.CurrentCulture;
        var alreadyGraded = bucket.Grades
            .Where(g => g.AssessmentId is not null && !g.IsVoided)
            .Select(g => g.AssessmentId)
            .ToHashSet();
        var choices = new List<AssessmentChoice> { new(null, NoAssessmentText, null) };
        choices.AddRange(bucket.Assessments
            .Where(a => !alreadyGraded.Contains(a.Id) && IsWithinEnrollment(a.Date))
            .Select(a => new AssessmentChoice(
                a.Id,
                string.Format(culture, "{0} ({1:d})", a.Title, a.Date.ToDateTime(TimeOnly.MinValue)),
                a.Date)));

        _addDefaultDate = DefaultDate();
        GradeDate = _addDefaultDate;
        AssessmentChoices = choices.ToObservableCollection();
        SelectedAssessment = choices[0];
        IsEditorOpen = true;
    }

    /// <summary>True when the student was active in the class on <paramref name="date"/> (the leave day is exclusive).</summary>
    private bool IsWithinEnrollment(DateOnly date) =>
        (_joinedOn is not { } joined || date >= joined) && (_leftOn is not { } left || date < left);

    private bool CanBeginEditGrade(StudentGradeRow? grade) =>
        IsWritable && !IsBusy && !HasPendingInput && grade is { CanEdit: true };

    /// <summary>Opens the editor for an existing grade, with its current values.</summary>
    [RelayCommand(CanExecute = nameof(CanBeginEditGrade))]
    private void BeginEditGrade(StudentGradeRow grade)
    {
        CancelInput();

        _editorBucketId = grade.BucketId;
        _editingAssessmentId = grade.AssessmentId;
        EditingAssessmentTitle = grade.AssessmentId is null ? null : grade.AssessmentTitle ?? string.Empty;
        EditorBucketName = string.Empty;
        EditorTitle = "Edit grade";

        _originalGradeText = grade.Token;
        _originalDate = grade.RecordedOn.ToDateTime(TimeOnly.MinValue);
        _originalLabel = grade.Label ?? string.Empty;
        _originalComment = grade.Comment ?? string.Empty;

        GradeText = grade.Token;
        GradeDate = _originalDate;
        GradeLabel = _originalLabel;
        GradeComment = _originalComment;
        EditingGradeId = grade.Id;
        IsEditorOpen = true;
    }

    private bool CanSaveGrade() =>
        IsWritable && !IsBusy && IsEditorOpen && GradingScale.TryParseToken(GradeText, out _) && GradeValidationMessage is null;

    /// <summary>Saves the editor's grade: a new single grade, a new assessment grade, or the change of an existing one.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveGrade))]
    private async Task SaveGradeAsync(CancellationToken cancellationToken = default)
    {
        if (_studentId is not { } studentId
            || GradeDate is not { } date
            || !GradingScale.TryParseToken(GradeText, out var value))
        {
            return;
        }

        var recordedOn = DateOnly.FromDateTime(date);
        var label = GradeLabel;
        var comment = GradeComment;
        var bucketId = _editorBucketId;
        var assessmentId = SelectedAssessment?.Id;

        Func<Task> save = EditingGradeId is { } gradeId
            ? () => gradeService.UpdateGradeAsync(gradeId, value, recordedOn, label, comment, cancellationToken)
            : assessmentId is { } pickedId
                ? () => gradeService.RecordAssessmentGradeAsync(pickedId, studentId, value, comment, cancellationToken)
                : () => gradeService.RecordGradeAsync(bucketId, studentId, value, recordedOn, label, comment, cancellationToken);

        var succeeded = await RunWriteAsync(
            save,
            "Grade has been saved.",
            "The grade could not be saved. Check the grade and the date and try again.",
            "The grade could not be saved because the school year is closed.",
            isStaleAfterReload: null);

        if (succeeded)
        {
            CancelInput();
            await ReloadAsync();
        }
    }

    private bool CanCancelGrade() => IsEditorOpen;

    /// <summary>Closes the editor and drops what it holds.</summary>
    [RelayCommand(CanExecute = nameof(CanCancelGrade))]
    private void CancelGrade() => CancelInput();

    private bool CanVoidGrade(StudentGradeRow? grade) => IsWritable && !IsBusy && grade is { CanVoid: true };

    /// <summary>Voids a grade so it stops counting; it stays in the list when voided grades are shown.</summary>
    [RelayCommand(CanExecute = nameof(CanVoidGrade))]
    private async Task VoidGradeAsync(StudentGradeRow grade, CancellationToken cancellationToken = default)
    {
        var succeeded = await RunWriteAsync(
            () => gradeService.VoidGradeAsync(grade.Id, cancellationToken),
            "Grade has been voided.",
            "The grade could not be voided.",
            "The grade could not be voided because the school year is closed.",
            () => FindGrade(grade.Id) is not { IsVoided: false });

        if (succeeded)
        {
            // Marked at once so it cannot be voided a second time before the reload arrives.
            grade.MarkVoided(clock.Now);
            if (EditingGradeId == grade.Id)
            {
                CancelInput();
            }

            await ReloadAsync();
        }
    }

    private bool CanRestoreGrade(StudentGradeRow? grade) => IsWritable && !IsBusy && grade is { CanRestore: true };

    /// <summary>Restores a voided grade so it counts again.</summary>
    [RelayCommand(CanExecute = nameof(CanRestoreGrade))]
    private async Task RestoreGradeAsync(StudentGradeRow grade, CancellationToken cancellationToken = default)
    {
        var succeeded = await RunWriteAsync(
            () => gradeService.RestoreGradeAsync(grade.Id, cancellationToken),
            "Grade has been restored.",
            "The grade could not be restored. Its assessment may have been voided or graded again.",
            "The grade could not be restored because the school year is closed.",
            () => FindGrade(grade.Id) is not { CanRestore: true });

        if (succeeded)
        {
            grade.MarkRestored();
            await ReloadAsync();
        }
    }

    /// <summary>
    /// Runs one write and reports the outcome with fixed text. Exception messages are diagnostic and are never
    /// shown. On a closed year the lock state is refreshed and the editor input kept. When
    /// <paramref name="isStaleAfterReload"/> is given, a rejected write reloads the list and then asks it whether
    /// the grade really changed elsewhere (stale row); only then is the "changed in the meantime" text shown,
    /// otherwise the operation's own failure message.
    /// </summary>
    private async Task<bool> RunWriteAsync(
        Func<Task> operation,
        string successMessage,
        string failureMessage,
        string closedYearMessage,
        Func<bool>? isStaleAfterReload = null)
    {
        IsBusy = true;
        try
        {
            await operation();
            ShowSuccess(successMessage);
            return true;
        }
        catch (ClosedYearException ex)
        {
            LogWriteRefused(ex);
            ShowError(closedYearMessage);

            // The year was locked while the page was open: refresh the lock state so the page turns read-only
            // instead of offering writes that fail again. The typed input stays.
            if (_yearLock is not null)
            {
                await _yearLock.RefreshAsync();
            }

            return false;
        }
        catch (InvariantViolationException ex) when (isStaleAfterReload is not null)
        {
            LogWriteRejected(ex);

            // Look at the real state before blaming a concurrent change: other rejections (an assessment that was
            // voided or graded again) are the operation's own failure.
            IsBusy = false;
            await ReloadAsync();
            ShowError(isStaleAfterReload() ? StaleGradeMessage : failureMessage);
            return false;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            LogWriteRejected(ex);
            ShowError(failureMessage);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWriteFailed(ex);
            ShowError(failureMessage);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private const string StaleGradeMessage = "This grade changed in the meantime, the list was refreshed.";

    /// <summary>The row of a grade in the current list, or null when it is not listed (for example a voided one that is hidden).</summary>
    private StudentGradeRow? FindGrade(int gradeId) =>
        Subjects.SelectMany(s => s.Buckets).SelectMany(b => b.Grades).FirstOrDefault(g => g.Id == gradeId);

    private async Task ReloadAsync()
    {
        var version = ++_loadVersion;

        if (_studentId is not { } studentId || _schoolYearId is not { } schoolYearId)
        {
            _loadFailed = false;
            _joinedOn = null;
            _leftOn = null;
            _yearStart = null;
            _yearEnd = null;
            Subjects = [];
            OnPropertyChanged(nameof(EmptyMessage));
            return;
        }

        IsLoading = true;
        try
        {
            StudentGradeOverview overview;
            try
            {
                overview = await gradeSheetService.GetStudentGradesAsync(studentId, schoolYearId, ShowVoidedGrades);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogLoadFailed(ex);
                if (version == _loadVersion)
                {
                    _loadFailed = true;
                    Subjects = [];
                    OnPropertyChanged(nameof(EmptyMessage));
                    ShowError("The grades could not be loaded.");
                }

                return;
            }

            if (version != _loadVersion)
            {
                return;
            }

            // Only the list is replaced; the editor keeps whatever the user typed.
            _loadFailed = false;
            _joinedOn = overview.JoinedOn;
            _leftOn = overview.LeftOn;
            _yearStart = overview.SchoolYearStart;
            _yearEnd = overview.SchoolYearEnd;
            Subjects = overview.Subjects.Select(s => new StudentSubjectRow(s)).ToObservableCollection();
            OnPropertyChanged(nameof(EmptyMessage));
            NotifyCommands();
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Today, held within the days the student was active in the class and the school year, so the default
    /// date is usually valid. The day the student left is not active (exclusive), so the last valid day is the
    /// one before it.
    /// </summary>
    private DateTime DefaultDate()
    {
        var today = DateOnly.FromDateTime(clock.Now);

        var earliest = _joinedOn;
        if (_yearStart is { } start && (earliest is null || start > earliest))
        {
            earliest = start;
        }

        var latest = _yearEnd;
        if (_leftOn is { } left && (latest is null || left.AddDays(-1) < latest))
        {
            latest = left.AddDays(-1);
        }

        if (latest is { } max && today > max)
        {
            today = max;
        }

        if (earliest is { } min && today < min)
        {
            today = min;
        }

        return today.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>True when two typed grades mean the same value, or are the same text when neither is on the scale.</summary>
    private static bool SameGrade(string typed, string original) =>
        GradingScale.TryParseToken(typed, out var a) && GradingScale.TryParseToken(original, out var b)
            ? a == b
            : typed.Trim() == original.Trim();

    private void NotifyInputChanged()
    {
        OnPropertyChanged(nameof(IsEditingGrade));
        OnPropertyChanged(nameof(IsAssessmentPicked));
        OnPropertyChanged(nameof(ShowAssessmentPicker));
        OnPropertyChanged(nameof(ShowEditingAssessment));
        OnPropertyChanged(nameof(ShowLabelField));
        OnPropertyChanged(nameof(IsDateReadOnly));
        OnPropertyChanged(nameof(GradeValidationMessage));
        OnPropertyChanged(nameof(HasGradeValidationMessage));
        OnPropertyChanged(nameof(HasPendingInput));
        OnPropertyChanged(nameof(ShowEditor));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        BeginAddGradeCommand.NotifyCanExecuteChanged();
        BeginEditGradeCommand.NotifyCanExecuteChanged();
        SaveGradeCommand.NotifyCanExecuteChanged();
        CancelGradeCommand.NotifyCanExecuteChanged();
        VoidGradeCommand.NotifyCanExecuteChanged();
        RestoreGradeCommand.NotifyCanExecuteChanged();
    }

    private void ShowSuccess(string message) =>
        snackbar.Show("Success", message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));

    private void ShowError(string message) =>
        snackbar.Show("Error", message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

    [LoggerMessage(Level = LogLevel.Error, Message = "Loading the student's grades failed.")]
    private partial void LogLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Changing a student grade failed unexpectedly.")]
    private partial void LogWriteFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A student grade change was refused because the school year is closed.")]
    private partial void LogWriteRefused(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A student grade change was rejected.")]
    private partial void LogWriteRejected(Exception exception);
}
