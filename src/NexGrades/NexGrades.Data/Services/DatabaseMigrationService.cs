using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data.Diagnostics;

namespace NexGrades.Data.Services;

/// <summary>
/// Brings the database schema up to date at startup by applying pending EF Core migrations. The App runs it before
/// any screen opens, so the rest of the app can assume a current schema. It holds no business rules.
/// </summary>
public partial class DatabaseMigrationService(IDbContextFactory<AppDbContext> dbContext, ILogger<DatabaseMigrationService> logger)
{
    /// <summary>
    /// Applies pending migrations. Tracked as
    /// <c>DatabaseMigrationService.Run</c>.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken = default) => RunAsync(null, cancellationToken);

    /// <summary>
    /// Applies pending migrations, running <paramref name="beforeMigratingExistingDatabase"/> first when an existing
    /// database is about to change, so the App can back it up. A new database, or one that is already up to date,
    /// does not run it. Tracked as <c>DatabaseMigrationService.Run</c>.
    /// </summary>
    /// <param name="beforeMigratingExistingDatabase">
    /// Runs only when migrations are pending and at least one migration was applied before. If it throws, nothing
    /// is migrated and its exception propagates unchanged and is not logged here: it is not a failed migration, and
    /// the caller reports it.
    /// </param>
    /// <param name="cancellationToken">Cancels the check, the step and the migration.</param>
    public Task RunAsync(Func<CancellationToken, Task>? beforeMigratingExistingDatabase, CancellationToken cancellationToken = default) =>
        NexGradesTelemetry.TrackAsync("DatabaseMigrationService.Run", async () =>
        {
            var inPreMigrationStep = false;
            try
            {
                await using var db = await dbContext.CreateDbContextAsync(cancellationToken);
                var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                LogCheckingMigrations(pendingMigrations.Count);

                if (pendingMigrations.Count > 0)
                {
                    if (beforeMigratingExistingDatabase is not null
                        && (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
                    {
                        LogRunningPreMigrationStep();
                        inPreMigrationStep = true;
                        await beforeMigratingExistingDatabase(cancellationToken);
                        inPreMigrationStep = false;
                    }

                    await db.Database.MigrateAsync(cancellationToken);
                    LogMigrationsApplied();
                }
                else
                {
                    LogUpToDate();
                }
            }
            catch (Exception ex) when (!inPreMigrationStep)
            {
                LogRunFailed(ex);
                throw;
            }
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking database migrations; {PendingCount} pending")]
    private partial void LogCheckingMigrations(int pendingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Running pre-migration step")]
    private partial void LogRunningPreMigrationStep();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private partial void LogMigrationsApplied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database already up to date")]
    private partial void LogUpToDate();

    [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed")]
    private partial void LogRunFailed(Exception exception);
}
