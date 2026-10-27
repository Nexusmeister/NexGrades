using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.Common;
using NexGrades.Data;
using NexGrades.Data.Entities;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Features.Classes;

/// <summary>
/// Hands the id of the class to edit from the overview to the (transient) <see cref="ClassViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class ClassEditRequest
{
    public int? ClassInstanceId { get; set; }
}

/// <summary>A student on a class roster; <paramref name="Period"/> is pre-formatted for the current UI culture.</summary>
public sealed record ClassStudentRow(int StudentId, string FullName, string Period);

public partial class ClassViewModel(
    INavigationService navigation,
    ISnackbarService snackbar,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ClassEditRequest editRequest) : ViewModel
{
    private int? _editingClassInstanceId;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _title = "AddClass";
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private ObservableCollection<ClassStudentRow> _activeStudents = [];
    [ObservableProperty] private ObservableCollection<ClassStudentRow> _leftStudents = [];

    public bool HasNoStudents => ActiveStudents.Count == 0 && LeftStudents.Count == 0;
    public bool HasLeftStudents => LeftStudents.Count > 0;

    partial void OnActiveStudentsChanged(ObservableCollection<ClassStudentRow> value) => OnPropertyChanged(nameof(HasNoStudents));

    partial void OnLeftStudentsChanged(ObservableCollection<ClassStudentRow> value)
    {
        OnPropertyChanged(nameof(HasNoStudents));
        OnPropertyChanged(nameof(HasLeftStudents));
    }

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
    private async Task SaveClass(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (_editingClassInstanceId is { } editId)
        {
            var existing = await db.ClassInstances.FirstOrDefaultAsync(ci => ci.Id == editId, cancellationToken);
            if (existing is null)
            {
                snackbar.Show("Error", "Class no longer exists.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
                return;
            }

            existing.Name = Name;
            await db.SaveChangesAsync(cancellationToken);
            snackbar.Show("Success", "Class has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
            return;
        }

        var activeYear = await db.SchoolYears.FirstOrDefaultAsync(sy => sy.Status == SchoolYearStatus.Active, cancellationToken);
        if (activeYear is null)
        {
            snackbar.Show("Error", "No active school year found.", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            return;
        }

        // A brand-new class (as opposed to one rolled forward from a prior year) starts life as its own
        // cohort: the ClassGroup and its first ClassInstance share the same name.
        var classGroup = new ClassGroup
        {
            CohortLabel = Name,
            FoundedInSchoolYearId = activeYear.Id,
        };
        db.ClassGroups.Add(classGroup);
        await db.SaveChangesAsync(cancellationToken);

        var classInstance = new ClassInstance
        {
            ClassGroupId = classGroup.Id,
            SchoolYearId = activeYear.Id,
            Name = Name,
        };
        db.ClassInstances.Add(classInstance);
        var saved = await db.SaveChangesAsync(cancellationToken);

        if (saved != 0)
        {
            snackbar.Show("Success", "Class has been saved", ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
            navigation.GoBack();
        }
        else
        {
            snackbar.Show("Error", "Class has not been saved", ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        navigation.GoBack();
    }
}
