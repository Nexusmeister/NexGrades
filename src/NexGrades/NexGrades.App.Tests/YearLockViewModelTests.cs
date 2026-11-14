using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.Data;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class YearLockViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static YearLockViewModel CreateViewModel(LockTestEnvironment env, SchoolYearLockState? initial) =>
        new(env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, initial);

    [Fact]
    public Task WritabilityChanged_IsRaised_WhenTheUnlockExpires() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var states = await env.Monitor.LoadClosedYearsAsync();
        var viewModel = CreateViewModel(env, states[0]);
        var raised = 0;
        viewModel.WritabilityChanged += (_, _) => Interlocked.Increment(ref raised);
        Assert.True(viewModel.IsUnlocked);

        env.Clock.Now = Now.AddMinutes(6);
        env.FireLastTimer();
        await LockTestEnvironment.WaitUntilAsync(() => Volatile.Read(ref raised) == 1);

        Assert.True(viewModel.IsLocked);
        Assert.False(viewModel.IsWritable);
        Assert.Equal(string.Empty, viewModel.UnlockReason);
    });

    [Fact]
    public async Task WritabilityChanged_IsNotRaised_ForAnotherYear()
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2024/25", new DateTime(2024, 8, 1));
        var otherId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var states = await env.Monitor.LoadClosedYearsAsync();
        var followed = states.Single(s => s.SchoolYearId != otherId);
        var viewModel = CreateViewModel(env, followed);
        var raised = 0;
        viewModel.WritabilityChanged += (_, _) => raised++;

        await env.Monitor.RelockAsync(otherId);

        Assert.Equal(0, raised);
        Assert.Equal(followed, viewModel.State);
        Assert.True(viewModel.IsLocked);
    }

    [Fact]
    public async Task ShowAsync_FollowsTheNewYear_AndRaisesWritabilityChanged()
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var viewModel = CreateViewModel(env, null);
        var raised = 0;
        viewModel.WritabilityChanged += (_, _) => raised++;
        Assert.False(viewModel.IsWritable);

        await viewModel.ShowAsync(yearId);

        Assert.True(viewModel.IsUnlocked);
        Assert.Equal("correction", viewModel.UnlockReason);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task UnlockedState_AllowsRelockButNotUnlock()
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var states = await env.Monitor.LoadClosedYearsAsync();

        var viewModel = CreateViewModel(env, states[0]);

        Assert.True(viewModel.IsWritable);
        Assert.False(viewModel.UnlockCommand.CanExecute(null));
        Assert.True(viewModel.RelockCommand.CanExecute(null));
    }

    private static YearLockViewModel CreateFailingRefreshViewModel(
        LockTestEnvironment env, SchoolYearLockState state, UnlockRequest? answer, int allowedContexts)
    {
        var failing = new FlakyContextFactory(env.Factory, allowedContexts);
        var monitor = new SchoolYearLockMonitor(
            new UnlockService(failing, env.Clock, NullLogger<UnlockService>.Instance),
            env.Clock,
            env.Snackbar,
            NullLogger<SchoolYearLockMonitor>.Instance,
            (_, _) => new NoopDisposable());
        return new YearLockViewModel(monitor, new FakeUnlockDialog(answer), env.Snackbar, env.Clock, state);
    }

    [Fact]
    public Task UnlockCommand_ReportsError_WhenTheStateRefreshAfterwardsFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1));
        var state = (await env.Monitor.LoadClosedYearsAsync()).Single();
        var until = Now.AddHours(1);

        // One context is enough for the unlock itself; the refresh that follows then fails.
        var viewModel = CreateFailingRefreshViewModel(env, state, new UnlockRequest(until, "correction"), allowedContexts: 1);

        await viewModel.UnlockCommand.ExecuteAsync(null);

        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
        await using var context = new AppDbContext(env.Options);
        var year = await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == yearId);
        Assert.Equal(until, year.UnlockedUntil);
    });

    [Fact]
    public Task RelockCommand_ReportsError_WhenTheStateRefreshAfterwardsFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var state = (await env.Monitor.LoadClosedYearsAsync()).Single();
        var viewModel = CreateFailingRefreshViewModel(env, state, null, allowedContexts: 1);

        await viewModel.RelockCommand.ExecuteAsync(null);

        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
        await using var context = new AppDbContext(env.Options);
        var year = await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == yearId);
        Assert.Null(year.UnlockedUntil);
    });

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
