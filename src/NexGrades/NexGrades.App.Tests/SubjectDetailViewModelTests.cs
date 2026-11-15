using Microsoft.EntityFrameworkCore;
using NexGrades.App.Features.Subjects;
using NexGrades.Data;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class SubjectDetailViewModelTests
{
    private static SubjectDetailViewModel Create(LockTestEnvironment env) =>
        new(FakeNavigationService.Create(), env.Snackbar, new SubjectService(env.Factory));

    private static async Task<int> CountSubjectsAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        return await context.Subjects.CountAsync();
    }

    [Fact]
    public void MaxLengths_MatchTheService()
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var viewModel = Create(env);

        Assert.Equal(SubjectService.MaxNameLength, viewModel.MaxNameLength);
        Assert.Equal(SubjectService.MaxShortCodeLength, viewModel.MaxShortCodeLength);
    }

    [Fact]
    public Task SaveCommand_SavesTheSubject_ForNameAndShortCodeAtTheLimitAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var viewModel = Create(env);
        viewModel.Name = new string('a', SubjectService.MaxNameLength);
        viewModel.ShortCode = new string('b', SubjectService.MaxShortCodeLength);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, await CountSubjectsAsync(env));
        Assert.Equal(["Success"], env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task SaveCommand_ShowsErrorAndSavesNothing_WhenNameIsTooLongAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var viewModel = Create(env);
        viewModel.Name = new string('a', SubjectService.MaxNameLength + 1);
        viewModel.ShortCode = "TS";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, await CountSubjectsAsync(env));
        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task SaveCommand_ShowsErrorAndSavesNothing_WhenShortCodeIsTooLongAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var viewModel = Create(env);
        viewModel.Name = "Testfach";
        viewModel.ShortCode = new string('b', SubjectService.MaxShortCodeLength + 1);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, await CountSubjectsAsync(env));
        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
    });
}
