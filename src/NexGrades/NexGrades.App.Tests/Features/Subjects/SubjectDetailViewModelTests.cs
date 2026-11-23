using Microsoft.EntityFrameworkCore;
using NexGrades.App.Features.Subjects;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests.Features.Subjects;

public class SubjectDetailViewModelTests
{
    private static SubjectDetailViewModel Create(LockTestEnvironment env) =>
        new(FakeNavigationService.Create(), env.Snackbar, new SubjectService(env.Factory), null!);

    private static async Task<int> CountSubjectsAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        return await context.Subjects.CountAsync();
    }

    private sealed class OpenableSubjectViewModel(LockTestEnvironment env, IContentDialogService dialogs)
        : SubjectDetailViewModel(FakeNavigationService.Create(), env.Snackbar, new SubjectService(env.Factory), dialogs)
    {
        public Task OpenAsync(int? subjectId) => OnNavigatedToAsync(subjectId);

        public Task OpenWithoutParameterAsync() => OnNavigatedToAsync((object?)null);
    }

    private static async Task<int> SeedSubjectAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        var subject = new Subject { Name = "Mathematik", ShortCode = "Ma" };
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();
        return subject.Id;
    }

    [Fact]
    public Task OnNavigatedToAsync_SetsAddState_WhenParameterIsNullAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);

        await viewModel.OpenWithoutParameterAsync();

        Assert.Equal("Add subject", viewModel.Title);
        Assert.False(viewModel.IsEditing);
        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.ShortCode);
    });

    [Fact]
    public Task OnNavigatedToAsync_LoadsSubjectAndSetsEditState_ForExistingIdAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var id = await SeedSubjectAsync(env);
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);

        await viewModel.OpenAsync(id);

        Assert.Equal("Edit subject", viewModel.Title);
        Assert.True(viewModel.IsEditing);
        Assert.Equal("Mathematik", viewModel.Name);
        Assert.Equal("Ma", viewModel.ShortCode);
    });

    [Fact]
    public Task OnNavigatedToAsync_ResetsToAddState_WhenOpenedAgainWithoutParameterAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var id = await SeedSubjectAsync(env);
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);
        await viewModel.OpenAsync(id);

        await viewModel.OpenWithoutParameterAsync();

        Assert.Equal("Add subject", viewModel.Title);
        Assert.False(viewModel.IsEditing);
        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.ShortCode);
    });

    [Fact]
    public Task OnNavigatedToAsync_ShowsErrorAndStaysInAddState_ForUnknownIdAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);

        await viewModel.OpenAsync(999);

        Assert.False(viewModel.IsEditing);
        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task SaveCommand_UpdatesTheSubjectInsteadOfAdding_WhenEditingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var id = await SeedSubjectAsync(env);
        var (dialogs, _) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);
        await viewModel.OpenAsync(id);
        viewModel.Name = "Mathe";
        viewModel.ShortCode = "M";

        await viewModel.SaveCommand.ExecuteAsync(null);

        await using var context = new AppDbContext(env.Options);
        var subject = await context.Subjects.AsNoTracking().SingleAsync();
        Assert.Equal(id, subject.Id);
        Assert.Equal("Mathe", subject.Name);
        Assert.Equal("M", subject.ShortCode);
        Assert.Equal(["Success"], env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task CanLeaveAsync_AllowsWithoutPrompt_WhenEditedSubjectIsUnchangedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var id = await SeedSubjectAsync(env);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);
        await viewModel.OpenAsync(id);

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Theory]
    [InlineData("Mathe", "Ma")]
    [InlineData("Mathematik", "M")]
    public Task CanLeaveAsync_PromptsAndBlocks_WhenEditedSubjectIsChangedAsync(string name, string shortCode) => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2025, 10, 1));
        var id = await SeedSubjectAsync(env);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableSubjectViewModel(env, dialogs);
        await viewModel.OpenAsync(id);
        viewModel.Name = name;
        viewModel.ShortCode = shortCode;

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

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
