using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NexGrades.App.Features.Students;

public sealed record ClassInstanceOption(int Id, string Name);

/// <summary>
/// Hands the id of the student to edit from the overview to the (transient) <see cref="StudentViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class StudentEditRequest
{
    public int? StudentId { get; set; }
}

/// <summary>
/// One note as shown in the list. Dates are pre-formatted with the current UI culture so the view does not
/// depend on XAML string formats.
/// </summary>
public sealed record NoteRow(
    int Id,
    string Text,
    string? Category,
    string CreatedOnDisplay,
    string? EditedOnDisplay,
    bool IsArchived,
    string? ArchivedOnDisplay);

/// <summary>A school year selectable on the notes tab, with its lock state.</summary>
/// <param name="IsClosed">The year is closed (whether or not it is currently unlocked).</param>
public sealed record NoteYearOption(int Id, string Label, bool IsWritable, bool IsClosed, DateTime? UnlockedUntil);

public partial class StudentViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    NoteService noteService,
    StudentEditRequest editRequest,
    IContentDialogService contentDialogService) : ViewModel
{
    private int? _editingStudentId;
    private string? _editingNoteCategory;
    private bool _suppressNoteReload;

    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private ObservableCollection<ClassInstanceOption> _classes = [];
    [ObservableProperty] private ClassInstanceOption? _selectedClass;
    [ObservableProperty] private string _title = "Add student";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoNotes))]
    private ObservableCollection<NoteRow> _notes = [];

    [ObservableProperty] private ObservableCollection<NoteYearOption> _noteYears = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearWritable))]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearLocked))]
    [NotifyPropertyChangedFor(nameof(IsSelectedYearUnlocked))]
    [NotifyPropertyChangedFor(nameof(UnlockedUntilMessage))]
    private NoteYearOption? _selectedNoteYear;

    [ObservableProperty] private bool _showArchivedNotes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    private string _noteText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingNote))]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    private int? _editingNoteId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNoteCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveNoteCommand))]
    private bool _isBusy;

    /// <summary>True when an existing student is being edited; notes are only available then.</summary>
    [ObservableProperty] private bool _isExistingStudent;

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

    public override async Task OnNavigatedToAsync()
    {
        _editingStudentId = editRequest.StudentId;
        editRequest.StudentId = null;

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

        FirstName = student.FirstName;
        LastName = student.LastName;
        Title = "Edit student";
        IsExistingStudent = true;

        var years = await noteService.GetNoteYearsForStudentAsync(studentId);
        var options = years
            .Select(y => new NoteYearOption(
                y.SchoolYearId, y.Label, y.IsWritable, y.Status == SchoolYearStatus.Closed, y.UnlockedUntil))
            .ToList();

        _suppressNoteReload = true;
        try
        {
            NoteYears = options.ToObservableCollection();
            var activeId = years.FirstOrDefault(y => y.Status == SchoolYearStatus.Active)?.SchoolYearId;
            SelectedNoteYear = options.FirstOrDefault(o => o.Id == activeId) ?? options.FirstOrDefault();
        }
        finally
        {
            _suppressNoteReload = false;
        }

        await ReloadNotesAsync();
    }

    partial void OnSelectedNoteYearChanged(NoteYearOption? value)
    {
        AddNoteCommand.NotifyCanExecuteChanged();
        SaveNoteCommand.NotifyCanExecuteChanged();
        StartEditNoteCommand.NotifyCanExecuteChanged();
        CancelEditNote();

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

    [RelayCommand]
    private async Task SaveStudent(CancellationToken cancellationToken = default)
    {
        if (_editingStudentId is { } editId)
        {
            // Editing changes the name only; class membership is managed through enrollments, not here.
            await using var editDb = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await editDb.Students.FirstOrDefaultAsync(s => s.Id == editId, cancellationToken);
            if (existing is null)
            {
                snackbar.Show("Error", "Student no longer exists.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                return;
            }

            existing.FirstName = FirstName;
            existing.LastName = LastName;
            await editDb.SaveChangesAsync(cancellationToken);

            snackbar.Show("Success", "Student has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
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

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var studentToAdd = new Student
        {
            FirstName = FirstName,
            LastName = LastName,
        };

        db.Students.Add(studentToAdd);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            // EnrollmentService works on a single context, so build it on this short-lived one rather than
            // injecting it (only a context factory is registered).
            var enrollmentService = new EnrollmentService(db);
            await enrollmentService.CreateEnrollmentAsync(studentToAdd.Id, SelectedClass.Id, DateTime.Now, cancellationToken);

            snackbar.Show("Success", "Student has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Student has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }

    private bool CanAddNote() =>
        IsSelectedYearWritable && !IsEditingNote && !IsBusy && IsNoteTextValid();

    private bool CanSaveNote() =>
        IsSelectedYearWritable && IsEditingNote && !IsBusy && IsNoteTextValid();

    private bool IsNoteTextValid()
    {
        var trimmed = NoteText.Trim();
        return trimmed.Length > 0 && trimmed.Length <= NoteService.MaxTextLength;
    }

    [RelayCommand(CanExecute = nameof(CanAddNote))]
    private async Task AddNote(CancellationToken cancellationToken = default)
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

    private bool CanStartEditNote(NoteRow? note) => IsSelectedYearWritable && note is { IsArchived: false };

    [RelayCommand(CanExecute = nameof(CanStartEditNote))]
    private void StartEditNote(NoteRow note)
    {
        NoteText = note.Text;
        _editingNoteCategory = note.Category;
        EditingNoteId = note.Id;
    }

    [RelayCommand(CanExecute = nameof(CanSaveNote))]
    private async Task SaveNote(CancellationToken cancellationToken = default)
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
        NoteText = string.Empty;
    }

    [RelayCommand]
    private async Task ArchiveNote(NoteRow note, CancellationToken cancellationToken = default)
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

    [RelayCommand]
    private async Task RestoreNote(NoteRow note, CancellationToken cancellationToken = default)
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
            return false;
        }
        catch (InvariantViolationException)
        {
            snackbar.Show("Error", failureMessage, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadNotesAsync()
    {
        if (_editingStudentId is not { } studentId || SelectedNoteYear is not { } year)
        {
            Notes = [];
            OnPropertyChanged(nameof(HasNoNotes));
            return;
        }

        var notes = await noteService.GetNotesForStudentAsync(
            studentId, year.Id, includeArchived: ShowArchivedNotes);

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
    }
}
