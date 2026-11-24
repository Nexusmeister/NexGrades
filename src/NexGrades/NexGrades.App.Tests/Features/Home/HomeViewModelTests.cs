using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

    /// <summary>Delegates to the real factory until told to fail, like a database that became unreadable.</summary>
    private sealed class FailingFactory(IDbContextFactory<AppDbContext> inner) : IDbContextFactory<AppDbContext>
    {
        public bool Fail { get; set; }

        public AppDbContext CreateDbContext() =>
            Fail ? throw new InvalidOperationException("Database unavailable.") : inner.CreateDbContext();

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Fail ? throw new InvalidOperationException("Database unavailable.") : inner.CreateDbContextAsync(cancellationToken);
    }

    private static HomeViewModel CreateViewModel(LockTestEnvironment env, INavigationService? navigation = null) =>
        CreateViewModel(env, env.Factory, navigation);

    private static HomeViewModel CreateViewModel(LockTestEnvironment env, IDbContextFactory<AppDbContext> factory, INavigationService? navigation = null, ILogger<HomeViewModel>? logger = null) =>
        new(
            new FakeUserService(),
            new SchoolYearService(factory, env.Clock, NullLogger<SchoolYearService>.Instance),
            new PageNavigator(navigation ?? FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { }),
            logger ?? NullLogger<HomeViewModel>.Instance);

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
            var year = await context.SchoolYears.SingleAsync();
            year.Status = SchoolYearStatus.Closed;
            await context.SaveChangesAsync();
        }

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_HidesTheReminderWithoutThrowing_WhenTheReadFails()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var factory = new FailingFactory(env.Factory) { Fail = true };
        var viewModel = CreateViewModel(env, factory);

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_HidesAVisibleReminderWithoutThrowing_WhenTheReadFailsOnTheNextVisit()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var factory = new FailingFactory(env.Factory);
        var viewModel = CreateViewModel(env, factory);
        await viewModel.OnNavigatedToAsync();
        Assert.True(viewModel.IsYearEndReminderVisible);

        factory.Fail = true;
        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ShowsTheReminderAgain_WhenTheReadRecoversAfterAFailure()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var factory = new FailingFactory(env.Factory) { Fail = true };
        var viewModel = CreateViewModel(env, factory);
        await viewModel.OnNavigatedToAsync();

        factory.Fail = false;
        await viewModel.OnNavigatedToAsync();

        Assert.True(viewModel.IsYearEndReminderVisible);
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

    [Fact]
    public async Task DismissYearEndReminderCommand_KeepsTheCardHidden_WhenPressedWhileTheFirstRefreshIsInFlight()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var viewModel = CreateViewModel(env, gate);
        var held = gate.HoldNextQueryAsync(out var release);
        var refresh = viewModel.RefreshYearEndReminderAsync();
        await held;

        viewModel.DismissYearEndReminderCommand.Execute(null);
        release();
        await refresh;

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task RefreshYearEndReminderAsync_KeepsTheCardHidden_AfterADismissalDuringTheFirstRefresh()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var viewModel = CreateViewModel(env, gate);
        var held = gate.HoldNextQueryAsync(out var release);
        var refresh = viewModel.RefreshYearEndReminderAsync();
        await held;
        viewModel.DismissYearEndReminderCommand.Execute(null);
        release();
        await refresh;

        await viewModel.RefreshYearEndReminderAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task DismissYearEndReminderCommand_StillHolds_WhenPressedWhileALaterRefreshIsInFlight()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var viewModel = CreateViewModel(env, gate);
        await viewModel.RefreshYearEndReminderAsync();
        Assert.True(viewModel.IsYearEndReminderVisible);
        var held = gate.HoldNextQueryAsync(out var release);
        var refresh = viewModel.RefreshYearEndReminderAsync();
        await held;

        viewModel.DismissYearEndReminderCommand.Execute(null);
        release();
        await refresh;

        Assert.False(viewModel.IsYearEndReminderVisible);
    }

    [Fact]
    public async Task RefreshYearEndReminderAsync_HidesTheCardAndLogsOneWarningWithoutRecordContent_WhenTheReadFails()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var factory = new FailingFactory(env.Factory) { Fail = true };
        var logger = new CapturingLogger<HomeViewModel>();
        var viewModel = CreateViewModel(env, factory, logger: logger);

        await viewModel.RefreshYearEndReminderAsync();

        Assert.False(viewModel.IsYearEndReminderVisible);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.DoesNotContain("2025/26", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("2025", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshYearEndReminderAsync_KeepsTheNewestResult_WhenAnOlderRefreshFinishesLast()
    {
        using var env = new LockTestEnvironment(EndDate.AddDays(-5));
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var viewModel = CreateViewModel(env, gate);

        // The older refresh has read an Active year in the window and is held; the year is then closed and the
        // newer refresh reads no reminder. The older answer arrives last and must not bring the card back.
        var held = gate.HoldNextQueryAsync(out var release);
        var older = viewModel.RefreshYearEndReminderAsync();
        await held;
        await using (var context = new AppDbContext(env.Options))
        {
            var year = await context.SchoolYears.SingleAsync();
            year.Status = SchoolYearStatus.Closed;
            await context.SaveChangesAsync();
        }

        await viewModel.RefreshYearEndReminderAsync();
        release();
        await older;

        Assert.False(viewModel.IsYearEndReminderVisible);
    }
}
