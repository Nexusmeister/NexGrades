using NexGrades.App.Core.YearLock;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class SchoolYearLockMonitorTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0);

    private static SchoolYearLockState State(int id, SchoolYearStatus status, DateTime? until) =>
        new(id, "2025/26", status, null, until, until is null ? null : "correction");

    [Fact]
    public void NextCheckDelay_ReturnsNull_ForNoStates() =>
        Assert.Null(SchoolYearLockMonitor.NextCheckDelay([], Now));

    [Fact]
    public void NextCheckDelay_ReturnsDelayWithSlack_ForOneUnlock() =>
        Assert.Equal(
            TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1),
            SchoolYearLockMonitor.NextCheckDelay([State(1, SchoolYearStatus.Closed, Now.AddMinutes(5))], Now));

    [Fact]
    public void NextCheckDelay_UsesTheEarliestUnlock()
    {
        var states = new[]
        {
            State(1, SchoolYearStatus.Closed, Now.AddMinutes(30)),
            State(2, SchoolYearStatus.Closed, Now.AddMinutes(10)),
            State(3, SchoolYearStatus.Closed, Now.AddMinutes(20)),
        };

        Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1), SchoolYearLockMonitor.NextCheckDelay(states, Now));
    }

    [Fact]
    public void NextCheckDelay_IsCappedAtOneHour() =>
        Assert.Equal(
            TimeSpan.FromHours(1),
            SchoolYearLockMonitor.NextCheckDelay([State(1, SchoolYearStatus.Closed, Now.AddDays(3))], Now));

    [Fact]
    public void NextCheckDelay_IgnoresExpiredUnlocks() =>
        Assert.Null(SchoolYearLockMonitor.NextCheckDelay(
            [State(1, SchoolYearStatus.Closed, Now.AddMinutes(-1)), State(2, SchoolYearStatus.Closed, Now)], Now));

    [Fact]
    public void NextCheckDelay_IgnoresYearsThatAreNotClosed() =>
        Assert.Null(SchoolYearLockMonitor.NextCheckDelay([State(1, SchoolYearStatus.Active, Now.AddMinutes(5))], Now));

    [Fact]
    public async Task LoadClosedYearsAsync_ArmsTheTimer_ForAnActiveUnlock()
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");

        await env.Monitor.LoadClosedYearsAsync();

        Assert.Equal(1, env.ScheduleCount);
        Assert.Equal(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), env.LastDelay);
    }

    [Fact]
    public async Task LoadClosedYearsAsync_ReportsAnEndedUnlockAsLocked_AndArmsNoTimer()
    {
        using var env = new LockTestEnvironment(Now);
        await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(-5), "correction");

        var states = await env.Monitor.LoadClosedYearsAsync();

        var state = Assert.Single(states);
        Assert.Null(state.UnlockedUntil);
        Assert.Null(state.UnlockReason);
        Assert.Equal(0, env.ScheduleCount);
    }

    [Fact]
    public Task Expiry_BroadcastsLockedState_AndAnnouncesOnce() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var received = new List<SchoolYearLockState>();
        env.Monitor.Register(received, static (list, state) => list.Add(state));
        await env.Monitor.LoadClosedYearsAsync();
        Assert.NotNull(received[^1].UnlockedUntil);

        env.Clock.Now = Now.AddMinutes(6);
        env.FireLastTimer();
        await LockTestEnvironment.WaitUntilAsync(() => env.FakeSnackbar.Titles.Count == 1);

        var last = received[^1];
        Assert.Equal(yearId, last.SchoolYearId);
        Assert.Null(last.UnlockedUntil);
        Assert.Null(last.UnlockReason);
        Assert.False(last.IsWritableAt(env.Clock.Now));

        // A second timer callback finds nothing left to announce.
        var broadcasts = received.Count;
        env.FireLastTimer();
        await Task.Delay(300);
        Assert.Single(env.FakeSnackbar.Titles);
        Assert.Equal(broadcasts, received.Count);
    });

    [Fact]
    public Task Expiry_DoesNotAnnounce_WhenTheUnlockWasExtendedElsewhere() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        await env.Monitor.LoadClosedYearsAsync();
        var scheduled = env.ScheduleCount;
        await env.SetUnlockedUntilAsync(yearId, Now.AddHours(2));

        env.Clock.Now = Now.AddMinutes(6);
        env.FireLastTimer();
        await LockTestEnvironment.WaitUntilAsync(() => env.ScheduleCount > scheduled);

        Assert.Empty(env.FakeSnackbar.Titles);
    });

    [Fact]
    public Task Expiry_DoesNotOverwriteAnUnlockMadeWhileTheCheckIsRunning() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "old reason");
        var received = new List<SchoolYearLockState>();
        env.Monitor.Register(received, static (list, state) => list.Add(state));
        await env.Monitor.LoadClosedYearsAsync();

        // The check reads the expired state, then stalls before it can apply it.
        env.Clock.Now = Now.AddMinutes(6);
        var entered = env.Factory.HoldNextDisposalAsync(out var release);
        env.FireLastTimer();
        await entered;

        var newUntil = env.Clock.Now.AddHours(1);
        await env.Monitor.UnlockAsync(yearId, newUntil, "new reason");
        var scheduled = env.ScheduleCount;
        release();
        await LockTestEnvironment.WaitUntilAsync(() => env.ScheduleCount > scheduled);

        var last = received[^1];
        Assert.Equal(newUntil, last.UnlockedUntil);
        Assert.Equal("new reason", last.UnlockReason);
        Assert.Empty(env.FakeSnackbar.Titles);
    });

    [Fact]
    public async Task RelockAsync_BroadcastsLockedState_AndArmsNoTimer()
    {
        using var env = new LockTestEnvironment(Now);
        var yearId = await env.SeedClosedYearAsync("2025/26", new DateTime(2025, 8, 1), Now.AddMinutes(5), "correction");
        var received = new List<SchoolYearLockState>();
        env.Monitor.Register(received, static (list, state) => list.Add(state));
        await env.Monitor.LoadClosedYearsAsync();
        var scheduled = env.ScheduleCount;

        await env.Monitor.RelockAsync(yearId);

        Assert.Null(received[^1].UnlockedUntil);
        Assert.Equal(scheduled, env.ScheduleCount);
    }
}
