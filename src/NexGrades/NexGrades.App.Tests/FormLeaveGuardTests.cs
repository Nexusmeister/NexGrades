using System.Reflection;
using NexGrades.App.Features.Classes;
using NexGrades.App.Features.Subjects;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests;

/// <summary>
/// Tests the leave guard of the class and subject forms: unchanged input leaves silently, changed input asks, the
/// answer decides, and Save or Cancel leave without a second prompt.
/// </summary>
[Collection(NavigationParametersState.Name)]
public class FormLeaveGuardTests
{
    private static readonly DateTime Now = new(2025, 10, 1);

    private sealed class OpenableClassViewModel(LockTestEnvironment env, INavigationService navigation, IContentDialogService dialogs)
        : ClassViewModel(navigation, env.Snackbar, env.Factory, new ClassService(env.Factory), null!, null!, null!, dialogs)
    {
        public Task OpenAsync(int? classInstanceId) => OnNavigatedToAsync(classInstanceId);
    }

    private static async Task<int> SeedClassAsync(LockTestEnvironment env, string name)
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
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = name };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        return instance.Id;
    }

    private static async Task<OpenableClassViewModel> OpenClassAsync(LockTestEnvironment env, INavigationService navigation, IContentDialogService dialogs)
    {
        var id = await SeedClassAsync(env, "5A");
        var viewModel = new OpenableClassViewModel(env, navigation, dialogs);
        await viewModel.OpenAsync(id);

        return viewModel;
    }

    private static SubjectDetailViewModel CreateSubject(LockTestEnvironment env, INavigationService navigation, IContentDialogService dialogs) =>
        new(navigation, env.Snackbar, new SubjectService(env.Factory), dialogs);

    // ClassViewModel

    [Fact]
    public Task ClassCanLeaveAsync_AllowsWithoutPrompt_WhenNameIsUnchangedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await OpenClassAsync(env, CountingNavigation.Create(out _), dialogs);

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_AllowsWithoutPrompt_WhenNewFormIsEmptyAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = new OpenableClassViewModel(env, CountingNavigation.Create(out _), dialogs);
        await viewModel.OpenAsync(null);

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_AllowsWithoutPrompt_WhenOnlyWhitespaceWasAddedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await OpenClassAsync(env, CountingNavigation.Create(out _), dialogs);

        viewModel.Name = " 5A ";

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_PromptsAndStays_WhenNameChangedAndUserKeepsEditingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await OpenClassAsync(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = "6A";

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_PromptsAndLeaves_WhenNameChangedAndUserDiscardsAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = await OpenClassAsync(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = "6A";

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task ClassCancelCommand_GoesBackWithoutPrompt_WhenNameIsUnchangedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var navigation = CountingNavigation.Create(out var counter);
        var viewModel = await OpenClassAsync(env, navigation, dialogs);

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.Shown);
        Assert.Equal(1, counter.BackCalls);
    });

    [Fact]
    public Task ClassCancelCommand_StaysOnPage_WhenNameChangedAndUserKeepsEditingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var navigation = CountingNavigation.Create(out var counter);
        var viewModel = await OpenClassAsync(env, navigation, dialogs);
        viewModel.Name = "6A";

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.Shown);
        Assert.Equal(0, counter.BackCalls);
    });

    [Fact]
    public Task ClassCancelCommand_GoesBackAndLeavesGuardOpen_WhenUserDiscardsAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var navigation = CountingNavigation.Create(out var counter);
        var viewModel = await OpenClassAsync(env, navigation, dialogs);
        viewModel.Name = "6A";

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, counter.BackCalls);
        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task ClassSaveCommand_GoesBackAndLeavesWithoutPrompt_AfterSavingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var navigation = CountingNavigation.Create(out var counter);
        var viewModel = await OpenClassAsync(env, navigation, dialogs);
        viewModel.Name = "6A";

        await viewModel.SaveClassCommand.ExecuteAsync(null);

        Assert.Equal(1, counter.BackCalls);
        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_PromptsAgain_WhenGoingBackAfterSaveFailedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var navigation = CountingNavigation.Create(out var counter);
        counter.GoBackResult = false;
        var viewModel = await OpenClassAsync(env, navigation, dialogs);
        viewModel.Name = "6A";

        await viewModel.SaveClassCommand.ExecuteAsync(null);

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task ClassCanLeaveAsync_PromptsAgain_AfterPageIsOpenedAgainAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = await OpenClassAsync(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = "6A";
        await viewModel.SaveClassCommand.ExecuteAsync(null);

        await viewModel.OpenAsync(null);
        viewModel.Name = "7A";

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    // SubjectDetailViewModel

    [Fact]
    public Task SubjectCanLeaveAsync_AllowsWithoutPrompt_WhenNothingWasTypedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out _), dialogs);

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task SubjectCanLeaveAsync_AllowsWithoutPrompt_WhenOnlyWhitespaceWasTypedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = "   ";

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Theory]
    [InlineData("Maths", "")]
    [InlineData("", "M")]
    public Task SubjectCanLeaveAsync_PromptsAndStays_WhenInputTypedAndUserKeepsEditingAsync(string name, string shortCode) => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = name;
        viewModel.ShortCode = shortCode;

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task SubjectCanLeaveAsync_PromptsAndLeaves_WhenInputTypedAndUserDiscardsAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out _), dialogs);
        viewModel.Name = "Maths";

        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task SubjectCancelCommand_GoesBackWithoutPrompt_WhenNothingWasTypedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out var counter), dialogs);

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.Shown);
        Assert.Equal(1, counter.BackCalls);
    });

    [Fact]
    public Task SubjectCancelCommand_StaysOnPage_WhenInputTypedAndUserKeepsEditingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out var counter), dialogs);
        viewModel.Name = "Maths";

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.Shown);
        Assert.Equal(0, counter.BackCalls);
    });

    [Fact]
    public Task SubjectCancelCommand_GoesBackAndLeavesGuardOpen_WhenUserDiscardsAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.Primary);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out var counter), dialogs);
        viewModel.Name = "Maths";

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, counter.BackCalls);
        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task SubjectSaveCommand_GoesBackAndLeavesWithoutPrompt_AfterSavingAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out var counter), dialogs);
        viewModel.Name = "Maths";
        viewModel.ShortCode = "M";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, counter.BackCalls);
        Assert.True(await viewModel.CanLeaveAsync());
        Assert.Equal(0, fake.Shown);
    });

    [Fact]
    public Task SubjectCanLeaveAsync_StillPrompts_WhenSaveWasRejectedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var viewModel = CreateSubject(env, CountingNavigation.Create(out var counter), dialogs);
        viewModel.Name = new string('a', SubjectService.MaxNameLength + 1);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, counter.BackCalls);
        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    [Fact]
    public Task SubjectCanLeaveAsync_PromptsAgain_WhenGoingBackAfterSaveFailedAsync() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (dialogs, fake) = FakeContentDialogService.Create(ContentDialogResult.None);
        var navigation = CountingNavigation.Create(out var counter);
        counter.GoBackResult = false;
        var viewModel = CreateSubject(env, navigation, dialogs);
        viewModel.Name = "Maths";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(await viewModel.CanLeaveAsync());
        Assert.Equal(1, fake.Shown);
    });

    /// <summary>An <see cref="INavigationService"/> that counts <c>GoBack</c> calls and answers them with <see cref="GoBackResult"/>.</summary>
    public class CountingNavigation : DispatchProxy
    {
        public int BackCalls { get; private set; }

        public bool GoBackResult { get; set; } = true;

        public static INavigationService Create(out CountingNavigation counter)
        {
            var proxy = Create<INavigationService, CountingNavigation>();
            counter = (CountingNavigation)(object)proxy;

            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                return null;
            }

            if (targetMethod.Name == nameof(INavigationService.GoBack))
            {
                BackCalls++;

                return GoBackResult;
            }

            return targetMethod.ReturnType.IsValueType && targetMethod.ReturnType != typeof(void)
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }
}
