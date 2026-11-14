using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.Students;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class StudentGradesViewModelTests
{
    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static readonly DateOnly Day = new(2025, 9, 15);

    private sealed record Ids(int YearId, int StudentId, int BucketId);

    private static async Task<Ids> SeedAsync(LockTestEnvironment env, decimal? existingGrade = null)
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
        var student = new Student { FirstName = "Test", LastName = "Alpha" };
        var enrollment = new Enrollment { Student = student, ClassInstance = classInstance, JoinedOn = new DateTime(2025, 8, 1) };
        context.AddRange(year, group, classInstance, subject, subjectInstance, bucket, student, enrollment);
        if (existingGrade is { } value)
        {
            context.Add(new Grade { GradeBucket = bucket, Student = student, Value = value, RecordedOn = Day });
        }

        await context.SaveChangesAsync();
        return new Ids(year.Id, student.Id, bucket.Id);
    }

    private static async Task<(StudentGradesViewModel ViewModel, YearLockViewModel YearLock)> CreateAsync(
        LockTestEnvironment env, Ids ids)
    {
        var viewModel = new StudentGradesViewModel(
            env.Snackbar,
            new GradeSheetService(env.Factory, env.Clock),
            new GradeService(env.Factory, env.Clock),
            env.Clock,
            NullLogger<StudentGradesViewModel>.Instance);
        var state = new SchoolYearLockState(ids.YearId, "2025/26", SchoolYearStatus.Active, null, null, null);
        var yearLock = new YearLockViewModel(env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, state);
        viewModel.Attach(yearLock);
        await viewModel.LoadAsync(ids.StudentId, ids.YearId);
        return (viewModel, yearLock);
    }

    [Fact]
    public Task LoadAsync_ShowsTheSubjectsOfTheStudentAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, existingGrade: 2.0m);

        var (viewModel, _) = await CreateAsync(env, ids);

        var subject = Assert.Single(viewModel.Subjects);
        Assert.Equal("Testkunde", subject.SubjectName);
        Assert.Single(subject.Buckets[0].Grades);
        Assert.True(viewModel.IsWritable);
    });

    [Theory]
    [InlineData("7")]
    [InlineData("2++")]
    [InlineData("abc")]
    public Task InvalidGradeText_DisablesSaveAndSetsAMessage_AndKeepsTheTextAsync(string text) => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);

        viewModel.GradeText = text;

        Assert.Equal(text, viewModel.GradeText);
        Assert.False(viewModel.SaveGradeCommand.CanExecute(null));
        Assert.True(viewModel.HasGradeValidationMessage);
        Assert.NotNull(viewModel.GradeValidationMessage);
    });

    [Fact]
    public Task EmptyGradeText_DisablesSave_WithoutAMessageUntilTheUserTypesAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);

        Assert.Equal(string.Empty, viewModel.GradeText);
        Assert.False(viewModel.SaveGradeCommand.CanExecute(null));
        Assert.False(viewModel.HasGradeValidationMessage);
    });

    [Fact]
    public Task ValidGradeText_EnablesSaveAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);

        viewModel.GradeText = "2-";

        Assert.True(viewModel.SaveGradeCommand.CanExecute(null));
        Assert.False(viewModel.HasGradeValidationMessage);
    });

    [Fact]
    public Task OnWritabilityChanged_KeepsTypedFieldsAndPendingInput_WhenTheYearGetsLockedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, yearLock) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);
        viewModel.GradeText = "2-";
        viewModel.GradeComment = "typed";
        Assert.True(viewModel.HasPendingInput);
        await using (var context = new AppDbContext(env.Options))
        {
            var year = await context.SchoolYears.FindAsync(ids.YearId);
            year!.Status = SchoolYearStatus.Closed;
            await context.SaveChangesAsync();
        }

        await yearLock.RefreshAsync();
        viewModel.OnWritabilityChanged();

        Assert.False(yearLock.IsWritable);
        Assert.False(viewModel.IsWritable);
        Assert.Equal("2-", viewModel.GradeText);
        Assert.Equal("typed", viewModel.GradeComment);
        Assert.True(viewModel.HasPendingInput);
        Assert.True(viewModel.ShowEditor);
        Assert.False(viewModel.SaveGradeCommand.CanExecute(null));
    });

    [Fact]
    public Task LoadAsync_KeepsTheEditorInput_WhenTheListIsReloadedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);
        viewModel.GradeText = "2-";
        viewModel.GradeLabel = "typed";

        await viewModel.LoadAsync(ids.StudentId, ids.YearId);

        Assert.Equal("2-", viewModel.GradeText);
        Assert.Equal("typed", viewModel.GradeLabel);
        Assert.True(viewModel.IsEditorOpen);
        Assert.True(viewModel.HasPendingInput);
    });

    [Fact]
    public Task HasPendingInput_IsFalseAfterASuccessfulSave_AndTheGradeIsPersistedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);
        viewModel.GradeText = "2-";
        Assert.True(viewModel.HasPendingInput);

        await viewModel.SaveGradeCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasPendingInput);
        Assert.False(viewModel.IsEditorOpen);
        Assert.Equal(string.Empty, viewModel.GradeText);
        await using var context = new AppDbContext(env.Options);
        var stored = Assert.Single(context.Grades.ToList());
        Assert.Equal(2.3m, stored.Value);
        Assert.Single(viewModel.Subjects[0].Buckets[0].Grades);
    });

    [Fact]
    public Task HasPendingInput_IsFalseWhenEditingStarts_AndTrueAfterChangingTheValueAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env, existingGrade: 2.0m);
        var (viewModel, _) = await CreateAsync(env, ids);

        viewModel.BeginEditGradeCommand.Execute(viewModel.Subjects[0].Buckets[0].Grades[0]);

        Assert.True(viewModel.IsEditingGrade);
        Assert.Equal("2", viewModel.GradeText);
        Assert.False(viewModel.HasPendingInput);

        viewModel.GradeText = "3";

        Assert.True(viewModel.HasPendingInput);

        viewModel.GradeText = "2";

        Assert.False(viewModel.HasPendingInput);
    });

    [Fact]
    public Task SaveGradeCommand_KeepsTheInputAndPersistsNothing_WhenTheDateIsOutsideTheEnrollmentAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var (viewModel, _) = await CreateAsync(env, ids);
        viewModel.BeginAddGradeCommand.Execute(viewModel.Subjects[0].Buckets[0]);
        viewModel.GradeText = "2-";
        viewModel.GradeDate = new DateTime(2025, 7, 1);

        await viewModel.SaveGradeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEditorOpen);
        Assert.Equal("2-", viewModel.GradeText);
        Assert.True(viewModel.HasPendingInput);
        Assert.Contains("Error", env.FakeSnackbar.Titles);
        await using var context = new AppDbContext(env.Options);
        Assert.Empty(context.Grades);
    });
}
