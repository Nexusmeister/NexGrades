using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.Grades;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests;

public class GradeSheetViewModelTests
{
    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static readonly DateOnly Day = new(2025, 9, 15);

    private sealed record Ids(int SubjectInstanceId, int StudentId, int GradeId);


    private static async Task<Ids> SeedAsync(LockTestEnvironment env, bool withGrade)
    {
        await using var context = new AppDbContext(env.Options);
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Active,
        };
        var group = new ClassGroup { CohortLabel = "Test cohort", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var subject = new Subject { Name = "Testkunde", ShortCode = "TK" };
        var subjectInstance = new SubjectInstance { ClassInstance = classInstance, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Written", Weight = 100m, SortOrder = 1 };
        var assessment = new Assessment { GradeBucket = bucket, Title = "Test 1", Date = Day };
        var student = new Student { FirstName = "Test", LastName = "Alpha" };
        var enrollment = new Enrollment { Student = student, ClassInstance = classInstance, JoinedOn = new DateTime(2025, 8, 1) };
        context.AddRange(year, group, classInstance, subject, subjectInstance, bucket, assessment, student, enrollment);
        Grade? grade = null;
        if (withGrade)
        {
            grade = new Grade { GradeBucket = bucket, Assessment = assessment, Student = student, Value = 2.0m, RecordedOn = Day };
            context.Add(grade);
        }

        await context.SaveChangesAsync();
        return new Ids(subjectInstance.Id, student.Id, grade?.Id ?? 0);
    }

    private static async Task<GradeSheetViewModel> CreateAsync(LockTestEnvironment env, Ids ids, IContentDialogService dialogs)
    {
        var yearLockFactory = new YearLockViewModelFactory(
            env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, NullLogger<YearLockViewModel>.Instance);
        var viewModel = new GradeSheetViewModel(
            FakeNavigationService.Create(),
            env.Snackbar,
            new GradeSheetService(env.Factory, env.Clock),
            new GradeService(env.Factory, env.Clock),
            new AssessmentService(env.Factory, env.Clock),
            dialogs,
            yearLockFactory,
            NullLogger<GradeSheetViewModel>.Instance);

        // OnNavigatedToAsync() takes its parameter from the navigation hand-over, which a test does not go through.
        await InvokeOnNavigatedToAsync(viewModel, ids.SubjectInstanceId);
        return viewModel;
    }

    private static Task InvokeOnNavigatedToAsync(GradeSheetViewModel viewModel, int subjectInstanceId)
    {
        var method = typeof(GradeSheetViewModel).GetMethod(
            "OnNavigatedToAsync", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(object)])!;
        return (Task)method.Invoke(viewModel, [subjectInstanceId])!;
    }

    private static GradeCellViewModel TheCell(GradeSheetViewModel viewModel) => viewModel.Rows.Single().Cells.Single();

    private static async Task<List<Grade>> LoadGradesAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        return await context.Grades.AsNoTracking().ToListAsync();
    }

    [Fact]
    public Task ShowVoidedGrades_KeepsTheTypedText_AndRevertsTheToggle_WhenTheUserDeclinesTheReload() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var rowsBefore = viewModel.Rows;
        TheCell(viewModel).Text = "x";
        Assert.True(viewModel.HasUnsavedInput);

        viewModel.ShowVoidedGrades = true;
        await LockTestEnvironment.WaitUntilAsync(() => !viewModel.ShowVoidedGrades);

        Assert.Equal(1, fake.Shown);
        Assert.Same(rowsBefore, viewModel.Rows);
        Assert.Equal("x", TheCell(viewModel).Text);
        Assert.True(viewModel.HasUnsavedInput);
    });

    [Fact]
    public Task ShowVoidedGrades_ReloadsTheSheet_WhenTheUserAcceptsDiscardingInput() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var rowsBefore = viewModel.Rows;
        TheCell(viewModel).Text = "x";

        viewModel.ShowVoidedGrades = true;
        await LockTestEnvironment.WaitUntilAsync(() => !ReferenceEquals(viewModel.Rows, rowsBefore) && !viewModel.IsLoading);

        Assert.Equal(1, fake.Shown);
        Assert.True(viewModel.ShowVoidedGrades);
        Assert.Equal(string.Empty, TheCell(viewModel).Text);
        Assert.False(viewModel.HasUnsavedInput);
    });

    [Fact]
    public Task ShowVoidedGrades_ReloadsWithoutAsking_WhenThereIsNoUnsavedInput() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var rowsBefore = viewModel.Rows;

        viewModel.ShowVoidedGrades = true;
        await LockTestEnvironment.WaitUntilAsync(() => !ReferenceEquals(viewModel.Rows, rowsBefore) && !viewModel.IsLoading);

        Assert.Equal(0, fake.Shown);
        Assert.True(viewModel.ShowVoidedGrades);
    });

    [Fact]
    public Task ReloadForVoidedToggleAsync_DoesNotRevert_WhenTheToggleWasChangedAgainMeanwhile() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await CreateAsync(env, ids, dialogs);
        TheCell(viewModel).Text = "x";

        // The toggle is off now, so a declined answer for "on" has nothing to put back.
        await viewModel.ReloadForVoidedToggleAsync(true);

        Assert.False(viewModel.ShowVoidedGrades);
        Assert.Equal("x", TheCell(viewModel).Text);
    });

    [Fact]
    public Task VoidAssessment_AsksOnce_AndReloads_WhenThereIsUnsavedInput() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var column = viewModel.Columns.Single(c => c.AssessmentId is not null);
        TheCell(viewModel).Text = "x";
        Assert.True(viewModel.HasUnsavedInput);

        await viewModel.VoidAssessmentCommand.ExecuteAsync(column);

        Assert.Equal(1, fake.Shown);
        Assert.False(viewModel.HasUnsavedInput);
        await using var context = new AppDbContext(env.Options);
        Assert.NotNull((await context.Assessments.AsNoTracking().SingleAsync()).VoidedOn);
    });

    [Fact]
    public Task VoidAssessment_AsksOnce_WhenThereIsNoUnsavedInput() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: false);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var column = viewModel.Columns.Single(c => c.AssessmentId is not null);

        await viewModel.VoidAssessmentCommand.ExecuteAsync(column);

        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task VoidCell_SendsNoSecondVoid_WhenTheCellIsEmptiedTypedAndEmptiedAgainDuringTheVoid() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, withGrade: true);
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await CreateAsync(env, ids, dialogs);
        var cell = TheCell(viewModel);
        Assert.Equal("2", cell.Text);

        // Holds the void's context after it has saved, so the user can edit while the call is still running.
        var entered = env.Factory.HoldNextDisposalAsync(out var release);
        try
        {
            cell.Text = string.Empty;
            await entered;
            cell.Text = "3";
            cell.Text = string.Empty;
        }
        finally
        {
            release();
        }

        await LockTestEnvironment.WaitUntilAsync(() => !cell.IsSaving);

        Assert.DoesNotContain("Error", env.FakeSnackbar.Titles);
        Assert.False(cell.HasError);
        Assert.Equal(string.Empty, cell.Text);
        Assert.Null(cell.Grade);
        var grades = await LoadGradesAsync(env);
        var grade = Assert.Single(grades);
        Assert.NotNull(grade.VoidedOn);
    });
}
