using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
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
public partial class StudentViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    StudentService studentService,
    NoteService noteService,
    IContentDialogService contentDialogService) : ViewModel
{
    private int? _editingStudentId;
    private string? _editingNoteCategory;
    private bool _suppressNoteReload;

    /// <summary>Set while a lock-state refresh re-selects the current year, so the editor keeps its text.</summary>
    private bool _keepEditorOnYearChange;
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

    [ObservableProperty] private string _title = "Add student";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoNotes))]
    private ObservableCollection<NoteRow> _notes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyNotesMessage))]
    private ObservableCollection<NoteYearOption> _noteYears = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearWritable))]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearLocked))]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearUnlocked))]
    [NotifyPropertyChangedFor(nameof(UnlockedUntilMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyNotesMessage))]
    private NoteYearOption? _selectedNoteYear;

    [ObservableProperty] private bool _showArchivedNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeNoteYear))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    private string _noteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingNote), nameof(NoteEditorLabel), nameof(CanChangeNoteYear))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    private int? _editingNoteId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartEditNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ArchiveNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreNoteCommand))]
    private bool _isBusy;

    /// <summary>True when an existing student is being edited; notes are only available then.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudentValidationMessage), nameof(HasStudentValidationMessage))]
    [NotifyCanExecuteChangedFor(nameof(SaveStudentCommand))]
    private bool _isExistingStudent;

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
                return "Enter a first name and a last name.";
            }

            if (IsExistingStudent)
            {
                return null;
            }

            if (Classes.Count == 0)
            {
                return "There is no class in the active school year to add the student to.";
            }

            return SelectedClass is null ? "Select a class for the student." : null;
        }
    }

    /// <summary>True when <see cref="StudentValidationMessage"/> has something to show.</summary>
    public bool HasStudentValidationMessage => StudentValidationMessage is not null;

    /// <summary>Explains an empty notes list, depending on why it is empty and whether notes can be added.</summary>
    public string EmptyNotesMessage =>
        NoteYears.Count == 0
            ? "This student is not enrolled in any school year yet, so there are no notes. Notes can be added once the student is enrolled in a class."
            : IsSelectedYearWritable
                ? "No notes for this school year yet. Write a note above to add one."
                : "No notes for this school year.";

    /// <summary>
    /// True when leaving the page would lose input: changed names, a class picked for a new student, or a
    /// note being written or edited.
    /// </summary>
    private bool HasUnsavedChanges =>
        FirstName != _loadedFirstName
        || LastName != _loadedLastName
        || (!IsExistingStudent && SelectedClass is not null)
        || HasPendingNoteInput;

    /// <summary>
    /// True while the note editor holds input that isn't saved: a new note being written, or an edited note
    /// whose text differs from what was loaded.
    /// </summary>
    private bool HasPendingNoteInput =>
        IsEditingNote ? NoteText != _editingNoteOriginalText : !string.IsNullOrWhiteSpace(NoteText);

    /// <summary>
    /// The school year can be switched only while the note editor holds no unsaved input, because switching
    /// resets the editor.
    /// </summary>
    public bool CanChangeNoteYear => !HasPendingNoteInput;

    /// <summary>Label of the note editor, which serves both adding and editing.</summary>
    public string NoteEditorLabel => IsEditingNote ? "Edit note" : "New note";

    /// <summary>True while the note editor holds an existing note rather than a new one.</summary>
    public bool IsEditingNote => EditingNoteId is not null;

    /// <summary>True when the list has nothing to show for the current year and archive filter.</summary>
    public bool HasNoNotes => Notes.Count == 0;

    /// <summary>Whether notes may be added or changed in the selected year right now.</summary>
    public bool IsSelectedYearWritable => SelectedNoteYear?.IsWritable ?? false;

    /// <summary>The selected year is closed and not unlocked: notes are read-only.</summary>
    public bool IsSelectedYearLocked => SelectedNoteYear is { IsClosed: true, IsWritable: false };

    /// <summary>The selected year is closed but temporarily unlocked: changes are audited.</summary>
    public bool IsSelectedYearUnlocked => SelectedNoteYear is { IsClosed: true, IsWritable: true };

    /// <summary>Explains an unlocked closed year to the user; empty when the year is not in that state.</summary>
    public string UnlockedUntilMessage =>
        SelectedNoteYear is { IsClosed: true, IsWritable: true, UnlockedUntil: { } until }
            ? string.Format(
                CultureInfo.CurrentCulture,
                "This school year is closed but unlocked until {0}. Changes are recorded in the audit log.",
                until.ToString("g", CultureInfo.CurrentCulture))
            : string.Empty;

    /// <summary>
    /// Loads the class choices and, when an existing student is being edited (the student id arrives as
    /// <paramref name="parameter"/>), the student and the notes.
    /// </summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        _editingStudentId = parameter as int?;

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
        Title = "Edit student";
        IsExistingStudent = true;

        await LoadNoteYearsAsync(studentId, keepYearId: null);
        await ReloadNotesAsync();
    }

    /// <summary>
    /// Loads the years the student can have notes in, with their current lock state. Selects
    /// <paramref name="keepYearId"/> if given and still present, otherwise the Active year, otherwise the newest.
    /// Does not reload the notes list itself.
    /// </summary>
    private async Task LoadNoteYearsAsync(int studentId, int? keepYearId)
    {
        var years = await noteService.GetNoteYearsForStudentAsync(studentId);
        var options = years
            .Select(y => new NoteYearOption(
                y.SchoolYearId, y.Label, y.IsWritable, y.Status == SchoolYearStatus.Closed, y.UnlockedUntil))
            .ToList();

        _suppressNoteReload = true;
        try
        {
            NoteYears = options.ToObservableCollection();
            var preferredId = keepYearId ?? years.FirstOrDefault(y => y.Status == SchoolYearStatus.Active)?.SchoolYearId;
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

        // Re-selecting the same year after a lock-state refresh must not throw away what the user typed.
        if (!_keepEditorOnYearChange)
        {
            CancelEditNote();
        }

        if (!_suppressNoteReload)
        {
            _ = ReloadNotesAsync();
        }
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
                "Missing class",
                "Select a class for the student before saving.",
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
            snackbar.Show("Error", "The student could not be saved. Check the selected class and try again.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show("Success", "Student has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        navigation.GoBack();
    }

    /// <summary>Saves the name change of the student being edited, then leaves the page.</summary>
    private async Task SaveEditedStudentAsync(int editId, CancellationToken cancellationToken)
    {
        if (HasPendingNoteInput
            && !await ConfirmDiscardAsync(
                "The note you are writing has not been saved. Save the student and discard the note?",
                cancellationToken))
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
            snackbar.Show("Error", "The student could not be saved.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show("Success", "Student has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        navigation.GoBack();
    }

    /// <summary>Goes back without saving, asking first when the page holds unsaved input.</summary>
    [RelayCommand]
    private async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (HasUnsavedChanges
            && !await ConfirmDiscardAsync(
                "You have unsaved changes on this page. Leave the page and discard them?", cancellationToken))
        {
            return;
        }

        navigation.GoBack();
    }

    /// <summary>Asks whether unsaved input may be discarded; true when the user agrees.</summary>
    private async Task<bool> ConfirmDiscardAsync(string content, CancellationToken cancellationToken)
    {
        var confirmation = await contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = "Discard changes",
                Content = content,
                PrimaryButtonText = "Discard",
                CloseButtonText = "Keep editing",
            },
            cancellationToken);

        return confirmation == ContentDialogResult.Primary;
    }

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
            "Note has been added.",
            "The note could not be saved.",
            "The note could not be saved because the school year is closed.");

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
            "Note has been saved.",
            "The note could not be saved.",
            "The note could not be saved because the school year is closed.");

        if (succeeded)
        {
            CancelEditNote();
            await ReloadNotesAsync();
        }
    }

    [RelayCommand]
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
                Title = "Archive note",
                Content = "Archive this note? Archived notes are hidden from the list. You can show and restore them at any time.",
                PrimaryButtonText = "Archive",
                CloseButtonText = "Cancel",
            },
            cancellationToken);

        if (confirmation != ContentDialogResult.Primary)
        {
            return;
        }

        var succeeded = await RunNoteOperationAsync(
            () => noteService.ArchiveNoteAsync(note.Id, cancellationToken),
            "Note has been archived.",
            "The note could not be archived.",
            "The note could not be archived because the school year is closed.");

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
            "Note has been restored.",
            "The note could not be restored.",
            "The note could not be restored because the school year is closed.");

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
            snackbar.Show("Success", successMessage, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            return true;
        }
        catch (ClosedYearException)
        {
            snackbar.Show("Error", closedYearMessage, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

            // The year was locked while the page was open (e.g. its unlock expired): refresh the lock state so
            // the page turns read-only instead of offering edits that will fail again.
            // The editor text is kept so the user can copy it or save it once the year is unlocked again.
            if (_editingStudentId is { } studentId)
            {
                _keepEditorOnYearChange = true;
                try
                {
                    await LoadNoteYearsAsync(studentId, SelectedNoteYear?.Id);
                    await ReloadNotesAsync();
                }
                catch (Exception)
                {
                    snackbar.Show("Error", "The school year state could not be refreshed.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                }
                finally
                {
                    _keepEditorOnYearChange = false;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show("Error", failureMessage, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
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
                snackbar.Show("Error", "The notes could not be loaded.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
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
                n.LastEditedOn is { } edited ? string.Format(culture, "Edited {0}", edited.ToString("g", culture)) : null,
                n.IsArchived,
                n.ArchivedOn is { } archived ? string.Format(culture, "Archived {0}", archived.ToString("g", culture)) : null))
            .ToObservableCollection();
        StartEditNoteCommand.NotifyCanExecuteChanged();
        ArchiveNoteCommand.NotifyCanExecuteChanged();
        RestoreNoteCommand.NotifyCanExecuteChanged();
    }
}
