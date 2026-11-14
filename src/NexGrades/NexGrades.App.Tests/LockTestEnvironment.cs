using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui;

namespace NexGrades.App.Tests;

/// <summary>
/// A real migrated SQLite file plus a <see cref="SchoolYearLockMonitor"/> wired to it with a manual scheduler, so
/// tests drive the expiry timer by hand instead of waiting for a dispatcher. Never creates a WPF application.
/// </summary>
internal sealed class LockTestEnvironment : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-lock-test-{Guid.NewGuid():N}.db");
    private readonly List<(TimeSpan Delay, Action Callback)> _scheduled = [];
    private readonly Lock _gate = new();

    public LockTestEnvironment(DateTime now)
    {
        Clock = new TestClock(now);
        Options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_dbPath}").Options;
        using (var context = new AppDbContext(Options))
        {
            context.Database.Migrate();
        }

        Factory = new GatedFactory(Options);
        var unlock = new UnlockService(Factory, Clock, NullLogger<UnlockService>.Instance);
        (Snackbar, FakeSnackbar) = FakeSnackbar.Create();
        Monitor = new SchoolYearLockMonitor(unlock, Clock, Snackbar, NullLogger<SchoolYearLockMonitor>.Instance, Schedule);
    }

    public TestClock Clock { get; }

    public DbContextOptions<AppDbContext> Options { get; }

    public GatedFactory Factory { get; }

    public ISnackbarService Snackbar { get; }

    public FakeSnackbar FakeSnackbar { get; }

    public SchoolYearLockMonitor Monitor { get; }

    /// <summary>How often the monitor armed its timer so far.</summary>
    public int ScheduleCount
    {
        get
        {
            lock (_gate)
            {
                return _scheduled.Count;
            }
        }
    }

    /// <summary>The delay of the most recent arming.</summary>
    public TimeSpan LastDelay
    {
        get
        {
            lock (_gate)
            {
                return _scheduled[^1].Delay;
            }
        }
    }

    /// <summary>Runs the callback of the most recently armed timer, as the dispatcher timer would on expiry.</summary>
    public void FireLastTimer()
    {
        Action callback;
        lock (_gate)
        {
            callback = _scheduled[^1].Callback;
        }

        callback();
    }

    public async Task<int> SeedClosedYearAsync(string label, DateTime start, DateTime? unlockedUntil = null, string? reason = null)
    {
        await using var context = new AppDbContext(Options);
        var year = new SchoolYear
        {
            Label = label,
            StartDate = start,
            EndDate = start.AddYears(1).AddDays(-1),
            Status = SchoolYearStatus.Closed,
            ClosedOn = start.AddYears(1),
            UnlockedUntil = unlockedUntil,
            UnlockReason = reason,
        };
        context.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    /// <summary>Sets the unlock expiry directly in the database, as another app instance would.</summary>
    public async Task SetUnlockedUntilAsync(int yearId, DateTime? unlockedUntil)
    {
        await using var context = new AppDbContext(Options);
        var year = await context.SchoolYears.FirstAsync(sy => sy.Id == yearId);
        year.UnlockedUntil = unlockedUntil;
        await context.SaveChangesAsync();
    }

    /// <summary>Polls until <paramref name="condition"/> holds; fails after a few seconds.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temp file.
        }
    }

    private NoopDisposable Schedule(TimeSpan delay, Action callback)
    {
        lock (_gate)
        {
            _scheduled.Add((delay, callback));
        }

        return new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A context factory whose next context can be held back when it is disposed, that is after its query has
    /// run. That freezes a read in flight with the answer it already has, which a later write makes stale.
    /// </summary>
    internal sealed class GatedFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        private readonly Lock _lock = new();
        private TaskCompletionSource? _release;
        private TaskCompletionSource? _entered;

        /// <summary>Holds back the disposal of the next context created.</summary>
        /// <param name="release">Lets the held disposal go on.</param>
        /// <returns>A task that completes once a context is being held.</returns>
        public Task HoldNextDisposalAsync(out Action release)
        {
            var releaseSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var enteredSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _release = releaseSource;
                _entered = enteredSource;
            }

            release = () => releaseSource.TrySetResult();
            return enteredSource.Task;
        }

        public AppDbContext CreateDbContext() => new GatedContext(options, Take());

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AppDbContext>(new GatedContext(options, Take()));

        private (TaskCompletionSource Release, TaskCompletionSource Entered)? Take()
        {
            lock (_lock)
            {
                if (_release is null || _entered is null)
                {
                    return null;
                }

                var taken = (_release, _entered);
                _release = null;
                _entered = null;
                return taken;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The test deliberately waits for a gate it controls.")]
        private sealed class GatedContext(
            DbContextOptions<AppDbContext> options, (TaskCompletionSource Release, TaskCompletionSource Entered)? hold)
            : AppDbContext(options)
        {
            public override async ValueTask DisposeAsync()
            {
                if (hold is { } held)
                {
                    held.Entered.TrySetResult();
                    await held.Release.Task;
                }

                await base.DisposeAsync();
            }
        }
    }
}
