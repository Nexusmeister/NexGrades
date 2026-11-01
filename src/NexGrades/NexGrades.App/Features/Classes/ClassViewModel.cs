using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Add and edit form for a class. Editing changes the class name only. A new class also gets its own class group,
/// so it starts a new cohort.
/// </summary>
/// <param name="navigation">Navigates back after a save or a cancel.</param>
/// <param name="snackbar">Reports the outcome of a save.</param>
/// <param name="dbContextFactory">Creates the short-lived context for loading the class and its roster.</param>
/// <param name="classService">Creates and renames the class.</param>
/// <param name="editRequest">Carries the id of the class to edit from the overview.</param>
public partial class ClassViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ClassService classService,
    ClassEditRequest editRequest) : ViewModel
{
    private int? _editingClassInstanceId;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _title = "AddClass";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private ObservableCollection<ClassStudentRow> _activeStudents = [];
    [ObservableProperty] private ObservableCollection<ClassStudentRow> _leftStudents = [];

    /// <summary>True when the roster has no students, neither active nor left.</summary>
    public bool HasNoStudents => ActiveStudents.Count == 0 && LeftStudents.Count == 0;
    /// <summary>True when at least one student has left the class.</summary>
    public bool HasLeftStudents => LeftStudents.Count > 0;

    partial void OnActiveStudentsChanged(ObservableCollection<ClassStudentRow> value) => OnPropertyChanged(nameof(HasNoStudents));

    partial void OnLeftStudentsChanged(ObservableCollection<ClassStudentRow> value)
    {
        OnPropertyChanged(nameof(HasNoStudents));
        OnPropertyChanged(nameof(HasLeftStudents));
    }

    /// <summary>Loads the class and its roster when the page opens for editing. Otherwise resets the form to the add state.</summary>
    public override async Task OnNavigatedToAsync()
    {
        _editingClassInstanceId = editRequest.ClassInstanceId;
        editRequest.ClassInstanceId = null;

        ActiveStudents = [];
        LeftStudents = [];
        IsEditing = false;
        Name = string.Empty;
        Title = "AddClass";

        if (_editingClassInstanceId is not { } id)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existing = await db.ClassInstances.AsNoTracking().FirstOrDefaultAsync(ci => ci.Id == id);
        if (existing is null)
        {
            _editingClassInstanceId = null;
            return;
        }

        Name = existing.Name;
        Title = "EditClass";
        IsEditing = true;

        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.ClassInstanceId == id)
            .OrderBy(e => e.Student.LastName).ThenBy(e => e.Student.FirstName)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName, e.JoinedOn, e.LeftOn })
            .ToListAsync();

        // Left students are listed separately so the active roster matches the class's current size.
        var culture = CultureInfo.CurrentCulture;
        ClassStudentRow ToRow(string first, string last, int studentId, DateTime joined, DateTime? left) => new(
            studentId,
            string.Format(culture, "{0}, {1}", last, first),
            left is { } l
                ? string.Format(culture, "{0:d} - {1:d}", joined, l)
                : string.Format(culture, "{0:d}", joined));

        ActiveStudents = enrollments.Where(e => e.LeftOn is null)
            .Select(e => ToRow(e.FirstName, e.LastName, e.StudentId, e.JoinedOn, null)).ToObservableCollection();
        LeftStudents = enrollments.Where(e => e.LeftOn is not null)
            .Select(e => ToRow(e.FirstName, e.LastName, e.StudentId, e.JoinedOn, e.LeftOn)).ToObservableCollection();
    }

    [RelayCommand]
    private async Task SaveClassAsync(CancellationToken cancellationToken = default)
    {
        var editId = _editingClassInstanceId;
        try
        {
            if (editId is { } id)
            {
                await classService.RenameClassAsync(id, Name, cancellationToken);
            }
            else
            {
                await classService.CreateClassAsync(Name, cancellationToken);
            }
        }
        catch (ClosedYearException)
        {
            snackbar.Show("Error", "The class could not be renamed because its school year is closed.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            snackbar.Show(
                "Error",
                editId is null
                ? "The class could not be created. Enter a name and make sure there is an active school year."
                : "The class could not be renamed. Enter a name, and check that the class has not been rolled forward or archived.",
                ControlAppearance.Caution,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
            return;
        }

        snackbar.Show("Success", "Class has been saved.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
        navigation.GoBack();
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }
}
