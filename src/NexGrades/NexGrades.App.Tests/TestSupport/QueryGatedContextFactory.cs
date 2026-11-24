using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NexGrades.Data;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>
/// Creates contexts on the given options and can hold back the next query right after the database answered it,
/// whatever its cancellation token says. That freezes a read in flight with the answer it already has, so a slow
/// older run can be made to finish after a newer one without any timing.
/// </summary>
internal sealed class QueryGatedContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly GateInterceptor _gate = new();

    public QueryGatedContextFactory(DbContextOptions<AppDbContext> options) =>
        _options = new DbContextOptionsBuilder<AppDbContext>(options).AddInterceptors(_gate).Options;

    /// <summary>Holds back the next query once its result is in.</summary>
    /// <param name="release">Lets the held query go on.</param>
    /// <returns>A task that completes once a query is being held.</returns>
    public Task HoldNextQueryAsync(out Action release) => _gate.ArmAsync(out release);

    public AppDbContext CreateDbContext() => new(_options);

    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new AppDbContext(_options));

    private sealed class GateInterceptor : DbCommandInterceptor
    {
        private readonly Lock _lock = new();
        private TaskCompletionSource? _release;
        private TaskCompletionSource? _entered;

        public Task ArmAsync(out Action release)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The test deliberately waits for a gate it controls.")]
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource? release;
            TaskCompletionSource? entered;
            lock (_lock)
            {
                release = _release;
                entered = _entered;
                _release = null;
                _entered = null;
            }

            if (release is not null && entered is not null)
            {
                entered.TrySetResult();
                await release.Task;
            }

            return result;
        }
    }
}
