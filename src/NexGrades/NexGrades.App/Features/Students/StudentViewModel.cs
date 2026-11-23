using System.Collections.ObjectModel;
using System.Data.Common;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.App.Core;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.Export;
using NexGrades.App.Infrastructure;
using NexGrades.App.Localization;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NexGrades.App.Features.Students;

/// <summary>
/// Add and edit form for a student, including the student's notes per school year. Notes are only available
/// for an existing student.
/// </summary>
/// <param name="navigation">Navigates back when the page is left.</param>
/// <param name="snackbar">Reports the outcome of each action.</param>
/// <param name="dbContextFactory">Creates the short-lived contexts for loading and saving the student.</param>
/// <param name="studentService">Creates a new student in the chosen class and renames an existing one.</param>
/// <param name="noteService">Lists, adds, edits, archives and restores notes.</param>
/// <param name="contentDialogService">Shows the confirmations for discarding input and for archiving notes.</param>
/// <param name="yearLockFactory">Creates the lock state of the selected school year, which gates note and grade writes.</param>
/// <param name="grades">The grades tab; shares the year picker and the year lock with the notes tab.</param>
/// <param name="noteExporter">Builds and writes the notes export.</param>
/// <param name="exportNotesDialog">Asks for the date range of the notes export.</param>
/// <param name="fileSaveDialog">Asks where to save the notes export.</param>
/// <param name="logger">Records unexpected export failures, without any student data.</param>
public partial class StudentViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    StudentService studentService,
    NoteService noteService,
    IContentDialogService contentDialogService,
    YearLockViewModelFactory yearLockFactory,
    StudentGradesViewModel grades,
    NoteExporter noteExporter,
    IExportNotesDialogService exportNotesDialog,
    IFileSaveDialogService fileSaveDialog,
    ILogger<StudentViewModel> logger) : ViewModel
{
    private int? _editingStudentId;
    private string? _editingNoteCategory;
    private bool _suppressNoteReload;

    // Set just before the page leaves by itself, so the leave guard does not ask a second time.
    private bool _allowLeave;
    private string _loadedFirstName = string.Empty;
    private string _loadedLastName = string.Empty;
    private string _editingNoteOriginalText = string.Empty;
    private int _notesReloadVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private ObservableCollection<ClassInstanceOption> _classes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private ClassInstanceOption? _selectedClass;

    [ObservableProperty] private string _title = TextResources.Text("Students_StudentPage_Add_Title");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoNotes))]
    private ObservableCollection<NoteRow> _notes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNotesMessage))]
    private ObservableCollection<NoteYearOption> _noteYears = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearWritable))]
    [NotifyPropertyChangedFor(nameof(EmptyNotesMessage))]
    [NotifyPropertyChangedFor(nameof(ShowNoteEditor))]
    private NoteYearOption? _selectedNoteYear;

    [ObservableProperty] private bool _showArchivedNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeNoteYear), nameof(ShowNoteEditor), nameof(CanDiscardNoteInput))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditNoteCommand))]
    private string _noteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingNote), nameof(NoteEditorLabel), nameof(CanChangeNoteYear), nameof(ShowNoteEditor), nameof(CanDiscardNoteInput))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelEditNoteCommand))]
    private int? _editingNoteId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportNotesCommand))]
    private bool _isBusy;

    /// <summary>True when an existing student is being edited; notes are only available then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private bool _isExistingStudent;

    /// <summary>Longest first or last name the form accepts; for the text boxes' <c>MaxLength</c> and a counter.</summary>
    public int MaxNameLength { get; } = StudentService.MaxNameLength;

    /// <summary>
    /// Why the student can't be saved yet, shown next to the form; <see langword="null"/> when the input is valid.
    /// Editing an existing student changes the names only, so the class is required for a new student alone.
    /// </summary>
    public string? StudentValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                return TextResources.Text("Students_StudentPage_Name_Required");
            }

            if (FirstName.Trim().Length > MaxNameLength)
            {
                return TextResources.Format("Students_StudentPage_FirstName_TooLong_Format", MaxNameLength);
            }

            if (LastName.Trim().Length > MaxNameLength)
            {
                return TextResources.Format("Students_StudentPage_LastName_TooLong_Format", MaxNameLength);
            }

            if (IsExistingStudent)
            {
                return null;
            }

            if (Classes.Count == 0)
            {
                return TextResources.Text("Students_StudentPage_NoClass_Available");
            }

            return SelectedClass is null ? TextResources.Text("Students_StudentPage_Class_Required") : null;
        }
    }

    /// <summary>True when <see cref="StudentValidationMessage"/> has something to show.</summary>
    public bool HasStudentValidationMessage => StudentValidationMessage is not null;

    /// <summary>Explains an empty notes list, depending on why it is empty and whether notes can be added.</summary>
    public string EmptyNotesMessage =>
        NoteYears.Count == 0
            ? TextResources.Text("Students_StudentPage_Notes_Empty_NotEnrolled")
            : IsSelectedYearWritable
                ? TextResources.Text("Students_StudentPage_Notes_Empty_Writable")
                : TextResources.Text("Students_StudentPage_Notes_Empty_ReadOnly");

    /// <summary>
    /// True when leaving the page would lose input: changed names, a class picked for a new student, or a
    /// note being written or edited.
    /// </summary>
    private bool HasUnsavedChanges =>
        FirstName != _loadedFirstName
        || LastName != _loadedLastName
        || (!IsExistingStudent && SelectedClass is not null)
        || HasPendingNoteInput
        || Grades.HasPendingInput;

    /// <summary>
    /// True while the note editor holds input that isn't saved: a new note being written, or an edited note
    /// whose text differs from what was loaded.
    /// </summary>
    private bool HasPendingNoteInput =>
        IsEditingNote ? NoteText != _editingNoteOriginalText : !string.IsNullOrWhiteSpace(NoteText);

    /// <summary>
    /// The school year can be switched only while neither the note editor nor the grade editor holds unsaved
    /// input, because switching resets them. The picker is shared by the notes and the grades tab.
    /// </summary>
    public bool CanChangeNoteYear => !HasPendingNoteInput && !Grades.HasPendingInput;

    /// <summary>The grades tab: the student's grades per subject and bucket, with its editor.</summary>
    public StudentGradesViewModel Grades { get; } = grades;

    /// <summary>Label of the note editor, which serves both adding and editing.</summary>
    public string NoteEditorLabel => IsEditingNote ? TextResources.Text("Students_StudentPage_NoteEditor_Edit") : TextResources.Text("Students_StudentPage_NoteEditor_New");

    /// <summary>True while the note editor holds an existing note rather than a new one.</summary>
    public bool IsEditingNote => EditingNoteId is not null;

    /// <summary>True when the list has nothing to show for the current year and archive filter.</summary>
    public bool HasNoNotes => Notes.Count == 0;

    /// <summary>
    /// The lock state of the selected school year. Notes can be written only while it is writable; the page shows
    /// its unlock and lock-again controls.
    /// </summary>
    public YearLockViewModel YearLock { get; } = yearLockFactory.Create();

    /// <summary>Whether notes may be added or changed in the selected year right now.</summary>
    public bool IsSelectedYearWritable => SelectedNoteYear is not null && YearLock.IsWritable;

    /// <summary>
    /// True when the note editor is shown: the selected year is writable, or the editor still holds input the
    /// user typed before the year turned read-only (an unlock that expired), which must stay visible so it can be
    /// copied or discarded.
    /// </summary>
    public bool ShowNoteEditor => IsSelectedYearWritable || HasPendingNoteInput;

    /// <summary>True when the editor holds a note being written or edited that can be thrown away.</summary>
    public bool CanDiscardNoteInput => IsEditingNote || HasPendingNoteInput;

    /// <summary>
    /// Loads the class choices and, when an existing student is being edited (the student id arrives as
    /// <paramref name="parameter"/>), the student and the notes.
    /// </summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        _allowLeave = false;
        _editingStudentId = parameter as int?;

        // Subscribed here rather than in a constructor; removing first keeps repeated visits from stacking handlers.
        YearLock.WritabilityChanged -= OnYearLockWritabilityChanged;
        YearLock.WritabilityChanged += OnYearLockWritabilityChanged;

        // The grades tab shares the year lock; it has no lock of its own. Its pending input blocks the year picker.
        Grades.Attach(YearLock);
        Grades.PropertyChanged -= OnGradesPropertyChanged;
        Grades.PropertyChanged += OnGradesPropertyChanged;

        await using var db = await dbContextFactory.CreateDbContextAsync();

        // Only classes of the currently Active school year can be assigned — enrolling into a Planned or
        // Closed year's class doesn't mean anything yet (no rollover/close workflow exists to have produced one).
        var classes = await db.ClassInstances
            .AsNoTracking()
            .Where(ci => ci.SchoolYear.Status == SchoolYearStatus.Active && ci.Status == ClassInstanceStatus.Active)
            .OrderBy(ci => ci.Name)
            .Select(ci => new ClassInstanceOption(ci.Id, ci.Name))
            .ToListAsync();

        Classes = classes.ToObservableCollection();

        if (_editingStudentId is not { } studentId)
        {
            return;
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null)
        {
            _editingStudentId = null;
            return;
        }

        _loadedFirstName = student.FirstName;
        _loadedLastName = student.LastName;
        FirstName = student.FirstName;
        LastName = student.LastName;
        Title = TextResources.Text("Students_StudentPage_Edit_Title");
        IsExistingStudent = true;

        await LoadNoteYearsAsync(studentId);
        await ReloadNotesAsync();
        await ReloadGradesAsync();
    }

    /// <summary>Loads the grades of the selected year; with no student or year, the grades tab shows its empty state.</summary>
    private Task ReloadGradesAsync() =>
        _editingStudentId is { } studentId
            ? Grades.LoadAsync(studentId, SelectedNoteYear?.Id)
            : Task.CompletedTask;

    private void OnGradesPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StudentGradesViewModel.HasPendingInput))
        {
            OnPropertyChanged(nameof(CanChangeNoteYear));
        }
    }

    /// <summary>
    /// Loads the years the student can have notes in and selects the Active year, otherwise the newest. The lock
    /// state of the selected year is read by <see cref="YearLock"/>. Does not reload the notes list itself.
    /// </summary>
    private async Task LoadNoteYearsAsync(int studentId)
    {
        var years = await noteService.GetNoteYearsForStudentAsync(studentId);
        var options = years
            .Select(y => new NoteYearOption(y.SchoolYearId, y.Label))
            .ToList();

        _suppressNoteReload = true;
        try
        {
            NoteYears = options.ToObservableCollection();
            var preferredId = years.FirstOrDefault(y => y.Status == SchoolYearStatus.Active)?.SchoolYearId;
            SelectedNoteYear = options.FirstOrDefault(o => o.Id == preferredId) ?? options.FirstOrDefault();
        }
        finally
        {
            _suppressNoteReload = false;
        }
    }

    partial void OnSelectedNoteYearChanged(NoteYearOption? value)
    {
        AddNoteCommand.NotifyCanExecuteChanged();
        SaveNoteCommand.NotifyCanExecuteChanged();
        StartEditNoteCommand.NotifyCanExecuteChanged();
        ArchiveNoteCommand.NotifyCanExecuteChanged();
        RestoreNoteCommand.NotifyCanExecuteChanged();
        ExportNotesCommand.NotifyCanExecuteChanged();

        // The year can only change while the editors hold no input (see CanChangeNoteYear), so this resets them.
        CancelEditNote();
        Grades.CancelInput();
        _ = YearLock.ShowAsync(value?.Id);

        if (!_suppressNoteReload)
        {
            _ = ReloadNotesAsync();
            _ = ReloadGradesAsync();
        }
    }

    /// <summary>Re-evaluates everything that depends on whether the selected year is writable.</summary>
    private void OnYearLockWritabilityChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsSelectedYearWritable));
        OnPropertyChanged(nameof(EmptyNotesMessage));
        OnPropertyChanged(nameof(ShowNoteEditor));
        AddNoteCommand.NotifyCanExecuteChanged();
        SaveNoteCommand.NotifyCanExecuteChanged();
        StartEditNoteCommand.NotifyCanExecuteChanged();
        ArchiveNoteCommand.NotifyCanExecuteChanged();
        RestoreNoteCommand.NotifyCanExecuteChanged();
        Grades.OnWritabilityChanged();
    }

    partial void OnShowArchivedNotesChanged(bool value)
    {
        if (!_suppressNoteReload)
        {
            _ = ReloadNotesAsync();
        }
    }

    private bool CanSaveStudent() => !IsBusy && StudentValidationMessage is null;

    [RelayCommand(CanExecute = nameof(CanSaveStudent))]
    private async Task SaveStudentAsync(CancellationToken cancellationToken = default)
    {
        if (_editingStudentId is { } editId)
        {
            await SaveEditedStudentAsync(editId, cancellationToken);
            return;
        }

        if (SelectedClass is null)
        {
            snackbar.Show(
                TextResources.Text("Students_StudentPage_Save_MissingClass_Title"),
                TextResources.Text("Students_StudentPage_Save_MissingClass_Message"),
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
            return;
        }

        try
        {
            await studentService.CreateStudentAsync(FirstName, LastName, SelectedClass.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(TextResources.Text("Common_Error"), TextResources.Text("Students_StudentPage_Create_Failed"), ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show(TextResources.Text("Common_Success"), TextResources.Text("Students_StudentPage_Save_Success"), ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        LeaveWithoutPrompt();
    }

    /// <summary>Saves the name change of the student being edited, then leaves the page.</summary>
    private async Task SaveEditedStudentAsync(int editId, CancellationToken cancellationToken)
    {
        var pendingMessage = (HasPendingNoteInput, Grades.HasPendingInput) switch
        {
            (true, true) => TextResources.Text("Students_StudentPage_Leave_Both"),
            (true, false) => TextResources.Text("Students_StudentPage_Leave_NoteOnly"),
            (false, true) => TextResources.Text("Students_StudentPage_Leave_GradeOnly"),
            _ => null,
        };
        if (pendingMessage is not null && !await ConfirmDiscardAsync(pendingMessage, cancellationToken))
        {
            return;
        }

        // Editing changes the name only; class membership is managed through enrollments, not here.
        try
        {
            await studentService.RenameStudentAsync(editId, FirstName, LastName, cancellationToken);
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(TextResources.Text("Common_Error"), TextResources.Text("Students_StudentPage_Save_Failed"), ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show(TextResources.Text("Common_Success"), TextResources.Text("Students_StudentPage_Save_Success"), ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        LeaveWithoutPrompt();
    }

    /// <summary>Goes back without saving, asking first when the page holds unsaved input.</summary>
    [RelayCommand]
    private async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (HasUnsavedChanges
            && !await ConfirmDiscardAsync(
                TextResources.Text("Core_DiscardChanges_Message"), cancellationToken))
        {
            return;
        }

        LeaveWithoutPrompt();
    }

    /// <summary>
    /// Asks before sidebar navigation or the back button leaves the page with unsaved input. Skipped once the page
    /// itself decided to leave (after saving, or after its own confirmation).
    /// </summary>
    public override async Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default)
    {
        if (_allowLeave || !HasUnsavedChanges)
        {
            return true;
        }

        return await ConfirmDiscardAsync(
            TextResources.Text("Core_DiscardChanges_Message"), cancellationToken);
    }

    /// <summary>Goes back without the leave guard asking a second time; re-arms the guard if going back fails.</summary>
    private void LeaveWithoutPrompt()
    {
        _allowLeave = true;
        if (!navigation.GoBack())
        {
            _allowLeave = false;
        }
    }

    /// <summary>Asks whether unsaved input may be discarded; true when the user agrees.</summary>
    private async Task<bool> ConfirmDiscardAsync(string content, CancellationToken cancellationToken)
    {
        var confirmation = await contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = TextResources.Text("Core_DiscardChanges_Title"),
                Content = content,
                PrimaryButtonText = TextResources.Text("Common_Discard"),
                CloseButtonText = TextResources.Text("Common_KeepEditing"),
            },
            cancellationToken);

        return confirmation == ContentDialogResult.Primary;
    }

    private bool CanExportNotes() => !IsBusy && _editingStudentId is not null && SelectedNoteYear is not null;

    /// <summary>
    /// Asks for a date range, then for a file, and exports the student's notes to it as an Excel workbook.
    /// Cancelling either dialog does nothing. Read-only, so it also works while the selected year is locked.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportNotes))]
    private async Task ExportNotesAsync(CancellationToken cancellationToken = default)
    {
        if (_editingStudentId is not { } studentId || SelectedNoteYear is not { } year)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var yearDates = await LoadYearDatesAsync(year.Id, cancellationToken);
            if (yearDates is null)
            {
                ShowExportError(TextResources.Text("Students_StudentPage_Export_Failed"));
                return;
            }

            var request = await exportNotesDialog.AskAsync(yearDates.Value.Start, yearDates.Value.End, cancellationToken);
            if (request is null)
            {
                return;
            }

            var table = await noteExporter.BuildAsync(studentId, request.From, request.To, request.IncludeArchived, cancellationToken);
            if (table.NoteCount == 0)
            {
                snackbar.Show(TextResources.Text("Students_StudentPage_Export_NoNotes_Title"), TextResources.Text("Students_StudentPage_Export_NoNotes_Message"), ControlAppearance.Info, new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(5));
                return;
            }

            var path = fileSaveDialog.SelectSaveFile(NoteExporter.SuggestFileName(table, CultureInfo.CurrentUICulture));
            if (path is null)
            {
                return;
            }

            await noteExporter.WriteAsync(table, path, cancellationToken);
            snackbar.Show(TextResources.Text("Common_Success"), TextResources.Format("Students_StudentPage_Export_Success_Format", Path.GetFileName(path)), ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // The page was left or the command cancelled; nothing to report.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowExportError(TextResources.Text("Students_StudentPage_Export_SaveFailed"));
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbException)
        {
            LogNotesExportFailed(ex);
            ShowExportError(TextResources.Text("Students_StudentPage_Export_Failed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The first and last day of a school year, the default range of the notes export; null if it is gone.</summary>
    private async Task<(DateOnly Start, DateOnly End)?> LoadYearDatesAsync(int schoolYearId, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dates = await db.SchoolYears
            .AsNoTracking()
            .Where(sy => sy.Id == schoolYearId)
            .Select(sy => new { sy.StartDate, sy.EndDate })
            .FirstOrDefaultAsync(cancellationToken);

        return dates is null ? null : (DateOnly.FromDateTime(dates.StartDate), DateOnly.FromDateTime(dates.EndDate));
    }

    private void ShowExportError(string message) =>
        snackbar.Show(TextResources.Text("Common_Error"), message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

    [LoggerMessage(Level = LogLevel.Error, Message = "Exporting the notes of a student failed.")]
    private partial void LogNotesExportFailed(Exception exception);

    private bool CanAddNote() =>
        IsSelectedYearWritable && !IsEditingNote && !IsBusy && IsNoteTextValid();

    private bool CanSaveNote() =>
        IsSelectedYearWritable && IsEditingNote && !IsBusy && IsNoteTextValid();

    private bool IsNoteTextValid()
    {
        var trimmed = NoteText.Trim();
        return trimmed.Length is > 0 and <= NoteService.MaxTextLength;
    }

    [RelayCommand(CanExecute = nameof(CanAddNote))]
    private async Task AddNoteAsync(CancellationToken cancellationToken = default)
    {
        if (_editingStudentId is not { } studentId || SelectedNoteYear is not { } year)
        {
            return;
        }

        var text = NoteText;
        var succeeded = await RunNoteOperationAsync(
            () => noteService.AddNoteAsync(studentId, year.Id, text, null, cancellationToken),
            TextResources.Text("Students_StudentPage_Note_Add_Success"),
            TextResources.Text("Students_StudentPage_Note_Save_Failed"),
            TextResources.Text("Students_StudentPage_Note_Save_ClosedYear"));

        if (succeeded)
        {
            NoteText = string.Empty;
            await ReloadNotesAsync();
        }
    }

    /// <summary>
    /// Editing a note loads it into the shared editor, so it is blocked while the editor holds unsaved input
    /// rather than silently overwriting it.
    /// </summary>
    private bool CanStartEditNote(NoteRow? note) =>
        IsSelectedYearWritable && !IsBusy && !HasPendingNoteInput && note is { IsArchived: false };

    [RelayCommand(CanExecute = nameof(CanStartEditNote))]
    private void StartEditNote(NoteRow note)
    {
        _editingNoteOriginalText = note.Text;
        _editingNoteCategory = note.Category;
        NoteText = note.Text;
        EditingNoteId = note.Id;
    }

    [RelayCommand(CanExecute = nameof(CanSaveNote))]
    private async Task SaveNoteAsync(CancellationToken cancellationToken = default)
    {
        if (EditingNoteId is not { } noteId)
        {
            return;
        }

        var text = NoteText;
        var category = _editingNoteCategory;
        var succeeded = await RunNoteOperationAsync(
            () => noteService.EditNoteAsync(noteId, text, category, cancellationToken),
            TextResources.Text("Students_StudentPage_Note_Save_Success"),
            TextResources.Text("Students_StudentPage_Note_Save_Failed"),
            TextResources.Text("Students_StudentPage_Note_Save_ClosedYear"));

        if (succeeded)
        {
            CancelEditNote();
            await ReloadNotesAsync();
        }
    }

    /// <summary>Throws away what the editor holds, whether a new note or an edit of an existing one.</summary>
    [RelayCommand(CanExecute = nameof(CanDiscardNoteInput))]
    private void CancelEditNote()
    {
        EditingNoteId = null;
        _editingNoteCategory = null;
        _editingNoteOriginalText = string.Empty;
        NoteText = string.Empty;
    }

    private bool CanChangeNoteState(NoteRow? note) => IsSelectedYearWritable && !IsBusy && note is not null;

    [RelayCommand(CanExecute = nameof(CanChangeNoteState))]
    private async Task ArchiveNoteAsync(NoteRow note, CancellationToken cancellationToken = default)
    {
        var confirmation = await contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = TextResources.Text("Students_StudentPage_Note_Archive_Title"),
                Content = TextResources.Text("Students_StudentPage_Note_Archive_Message"),
                PrimaryButtonText = TextResources.Text("Students_StudentPage_Note_Archive_Confirm"),
                CloseButtonText = TextResources.Text("Common_Cancel"),
            },
            cancellationToken);

        if (confirmation != ContentDialogResult.Primary)
        {
            return;
        }

        var succeeded = await RunNoteOperationAsync(
            () => noteService.ArchiveNoteAsync(note.Id, cancellationToken),
            TextResources.Text("Students_StudentPage_Note_Archive_Success"),
            TextResources.Text("Students_StudentPage_Note_Archive_Failed"),
            TextResources.Text("Students_StudentPage_Note_Archive_ClosedYear"));

        if (succeeded)
        {
            if (EditingNoteId == note.Id)
            {
                CancelEditNote();
            }

            await ReloadNotesAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanChangeNoteState))]
    private async Task RestoreNoteAsync(NoteRow note, CancellationToken cancellationToken = default)
    {
        var succeeded = await RunNoteOperationAsync(
            () => noteService.RestoreNoteAsync(note.Id, cancellationToken),
            TextResources.Text("Students_StudentPage_Note_Restore_Success"),
            TextResources.Text("Students_StudentPage_Note_Restore_Failed"),
            TextResources.Text("Students_StudentPage_Note_Restore_ClosedYear"));

        if (succeeded)
        {
            await ReloadNotesAsync();
        }
    }

    /// <summary>
    /// Runs one note operation and reports the outcome with fixed, translatable text. Exception messages are
    /// diagnostic and are never shown.
    /// </summary>
    private async Task<bool> RunNoteOperationAsync(
        Func<Task> operation, string successMessage, string failureMessage, string closedYearMessage)
    {
        IsBusy = true;
        try
        {
            await operation();
            snackbar.Show(TextResources.Text("Common_Success"), successMessage, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            return true;
        }
        catch (ClosedYearException)
        {
            snackbar.Show(TextResources.Text("Common_Error"), closedYearMessage, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

            // The year was locked while the page was open (e.g. its unlock expired): refresh the lock state so the
            // page turns read-only instead of offering edits that will fail again. The editor text stays, so the
            // user can copy it or save it once the year is unlocked again.
            await YearLock.RefreshAsync();

            return false;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(TextResources.Text("Common_Error"), failureMessage, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Reloads the notes list for the selected year and archive filter. Reloads can overlap (switching year or
    /// the filter quickly), so only the newest one may write its result; older ones are dropped. Failures are
    /// reported rather than thrown, because this also runs fire-and-forget from property-change handlers.
    /// </summary>
    private async Task ReloadNotesAsync()
    {
        var version = ++_notesReloadVersion;

        if (_editingStudentId is not { } studentId || SelectedNoteYear is not { } year)
        {
            Notes = [];
            OnPropertyChanged(nameof(HasNoNotes));
            return;
        }

        IReadOnlyList<NoteDto> notes;
        try
        {
            notes = await noteService.GetNotesForStudentAsync(
                studentId, year.Id, includeArchived: ShowArchivedNotes);
        }
        catch (Exception)
        {
            if (version == _notesReloadVersion)
            {
                snackbar.Show(TextResources.Text("Common_Error"), TextResources.Text("Students_StudentPage_Notes_LoadFailed"), ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            }

            return;
        }

        if (version != _notesReloadVersion)
        {
            return;
        }

        var culture = CultureInfo.CurrentCulture;
        Notes = notes
            .Select(n => new NoteRow(
                n.Id,
                n.Text,
                n.Category,
                n.CreatedOn.ToString("g", culture),
                n.LastEditedOn is { } edited ? TextResources.Format("Students_StudentPage_Note_Edited_Format", edited.ToString("g", culture)) : null,
                n.IsArchived,
                n.ArchivedOn is { } archived ? TextResources.Format("Students_StudentPage_Note_Archived_Format", archived.ToString("g", culture)) : null))
            .ToObservableCollection();
        StartEditNoteCommand.NotifyCanExecuteChanged();
        ArchiveNoteCommand.NotifyCanExecuteChanged();
        RestoreNoteCommand.NotifyCanExecuteChanged();
    }
}
