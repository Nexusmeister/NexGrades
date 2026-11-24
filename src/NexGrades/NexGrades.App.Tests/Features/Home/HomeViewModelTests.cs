using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Features.Home;
using NexGrades.App.Features.SchoolYears;
using NexGrades.App.Infrastructure;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;

namespace NexGrades.App.Tests.Features.Home;

[Collection(NavigationParametersState.Name)]
public class HomeViewModelTests
{
    private static readonly DateTime EndDate = new(2026, 7, 31);

    private sealed class FakeUserService : IUserService
    {
        public string GetUserName() => "Ada";
    }

    /// <summary>Records the page types passed to <c>NavigateWithHierarchy</c> and reports success.</summary>
    public class RecordingNavigationService : DispatchProxy
    {
        public IList<Type> Visited { get; } = [];

        public static INavigationService Create(out RecordingNavigationService recorder)
        {
            var proxy = Create<INavigationService, RecordingNavigationService>();
            recorder = (RecordingNavigationService)(object)proxy;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is { Name: nameof(INavigationService.NavigateWithHierarchy) } && args is [Type page, ..])
            {
                Visited.Add(page);
                return true;
            }

            return targetMethod is { ReturnType: { IsValueType: true } type } && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }

    private static HomeViewModel CreateViewModel(LockTestEnvironment env, INavigationService? navigation = null) =>
        new(
            new FakeUserService(),
            new SchoolYearService(env.Factory, env.Clock, NullLogger<SchoolYearService>.Instance),
            new PageNavigator(navigation ?? FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { }));

    private static async Task SeedAsync(LockTestEnvironment env, SchoolYearStatus status = SchoolYearStatus.Active)
    {
        await using var context = new AppDbContext(env.Options);
        context.SchoolYears.Add(new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = EndDate,
            Status = status,
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task OnNavigatedToAsync_ShowsTheReminderWithTheEndDate_InsideTheWindow()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-10).AddHours(9));
        await SeedAsync(env);
        var viewModel = CreateViewModel(env);

        await viewModel.OnNavigatedToAsync();

        Assert.True(viewModel.IsYearEndReminderVisible);
        Assert.Contains("2025/26", viewModel.YearEndReminderMessage, StringComparison.Ordinal);
        Assert.Contains("ends on", viewModel.YearEndReminderMessage, StringComparison.Ordinal);
        Assert.Contains("7/31/2026", viewModel.YearEndReminderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnNavigatedToAsync_WordsTheReminderAsEnded_AfterTheEndDate()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(3));
        await SeedAsync(env);
        var viewModel = CreateViewModel(env);

        await viewModel.OnNavigatedToAsync();

        Assert.True(viewModel.IsYearEndReminderVisible);
        Assert.Contains("has not been closed yet", viewModel.YearEndReminderMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnNavigatedToAsync_HidesTheReminder_BeforeTheWindow()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-29));
        await SeedAsync(env);
        var viewModel = CreateViewModel(env);

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_HidesTheReminder_WithoutAnActiveYear()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-1));
        await SeedAsync(env, SchoolYearStatus.Planned);
        var viewModel = CreateViewModel(env);

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_HidesTheReminder_WhenTheYearGotClosedSinceTheLastVisit()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-1));
        await SeedAsync(env);
        var viewModel = CreateViewModel(env);
        await viewModel.OnNavigatedToAsync();
        Assert.True(viewModel.IsYearEndReminderVisible);

        await using (var context = new AppDbContext(env.Options))
        {
            var year = context.SchoolYears.Single();
            year.Status = SchoolYearStatus.Closed;
            await context.SaveChangesAsync();
        }

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task DismissYearEndReminderCommand_HidesTheCard_AndItStaysHiddenOnTheNextVisit()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var viewModel = CreateViewModel(env);
        await viewModel.OnNavigatedToAsync();
        Assert.True(viewModel.IsYearEndReminderVisible);

        viewModel.DismissYearEndReminderCommand.Execute(null);
        Assert.False(viewModel.IsYearEndReminderVisible);

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ShowsTheReminderAgain_ForANewViewModelAfterDismissal()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var first = CreateViewModel(env);
        await first.OnNavigatedToAsync();
        first.DismissYearEndReminderCommand.Execute(null);

        var second = CreateViewModel(env);
        await second.OnNavigatedToAsync();

        Assert.True(second.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OpenEndOfYearCommand_NavigatesToTheSchoolYearPage_WithTheStartRequest()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var navigation = RecordingNavigationService.Create(out var recorder);
        var viewModel = CreateViewModel(env, navigation);
        await viewModel.OnNavigatedToAsync();

        viewModel.OpenEndOfYearCommand.Execute(null);

        Assert.Equal([typeof(SchoolYearPage)], recorder.Visited);
        Assert.Equal(SchoolYearPageRequest.StartEndOfYearFlow, NavigationParameters.Take(typeof(SchoolYearViewModel)));
    }
}
