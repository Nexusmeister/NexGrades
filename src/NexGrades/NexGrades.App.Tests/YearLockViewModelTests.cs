using NexGrades.App.Core.YearLock;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class YearLockViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static YearLockViewModel CreateViewModel(LockTestEnvironment env, SchoolYearLockState? initial) =>
        new(env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, initial);

    [Fact]
    public async Task WritabilityChanged_IsRaised_WhenTheUnlockExpiresAsync()
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
    }

    [Fact]
    public async Task WritabilityChanged_IsNotRaised_ForAnotherYearAsync()
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
    public async Task ShowAsync_FollowsTheNewYear_AndRaisesWritabilityChangedAsync()
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
    public async Task UnlockedState_AllowsRelockButNotUnlockAsync()
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var states = await env.Monitor.LoadClosedYearsAsync();

        var viewModel = CreateViewModel(env, states[0]);

        Assert.True(viewModel.IsWritable);
        Assert.False(viewModel.UnlockCommand.CanExecute(null));
        Assert.True(viewModel.RelockCommand.CanExecute(null));
    }
}
