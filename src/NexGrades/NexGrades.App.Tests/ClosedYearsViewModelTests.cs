using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.SchoolYears;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class ClosedYearsViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static (ClosedYearsViewModel ViewModel, FlakyContextFactory Database) Create(LockTestEnvironment env, int allowedContexts)
    {
        var flaky = new FlakyContextFactory(env.Factory, allowedContexts);
        var monitor = new SchoolYearLockMonitor(
            new UnlockService(flaky, env.Clock, NullLogger<UnlockService>.Instance),
            env.Clock,
            env.Snackbar,
            NullLogger<SchoolYearLockMonitor>.Instance,
            (_, _) => new NoopDisposable());
        var factory = new YearLockViewModelFactory(
            monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, NullLogger<YearLockViewModel>.Instance);
        return (new ClosedYearsViewModel(monitor, factory, env.Snackbar), flaky);
    }

    [Fact]
    public Task ReloadAsync_SetsLoadFailed_AndHidesTheNoClosedYearsHint_WhenTheLoadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (viewModel, _) = Create(env, allowedContexts: 0);

        await viewModel.ReloadAsync();

        Assert.True(viewModel.LoadFailed);
        Assert.False(viewModel.ShowNoClosedYears);
        Assert.False(viewModel.HasClosedYears);
        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task ReloadAsync_ShowsTheNoClosedYearsHint_WhenTheLoadSucceedsWithoutClosedYears() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var (viewModel, _) = Create(env, allowedContexts: 1);

        await viewModel.ReloadAsync();

        Assert.False(viewModel.LoadFailed);
        Assert.True(viewModel.ShowNoClosedYears);
        Assert.Empty(env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task ReloadAsync_ResetsLoadFailed_WhenALaterLoadSucceeds() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1));
        var (viewModel, database) = Create(env, allowedContexts: 0);
        await viewModel.ReloadAsync();
        Assert.True(viewModel.LoadFailed);

        database.Allow(1);
        await viewModel.ReloadAsync();

        Assert.False(viewModel.LoadFailed);
        Assert.False(viewModel.ShowNoClosedYears);
        Assert.Single(viewModel.Years);
    });

    [Fact]
    public Task ReloadAsync_KeepsTheEarlierList_WhenALaterLoadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1));
        var (viewModel, _) = Create(env, allowedContexts: 1);
        await viewModel.ReloadAsync();

        await viewModel.ReloadAsync();

        Assert.True(viewModel.LoadFailed);
        Assert.False(viewModel.ShowNoClosedYears);
        Assert.Single(viewModel.Years);
    });

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
