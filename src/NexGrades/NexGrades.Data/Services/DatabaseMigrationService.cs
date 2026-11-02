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
    public Task RunAsync(CancellationToken cancellationToken = default) =>
        NexGradesTelemetry.TrackAsync("DatabaseMigrationService.Run", async () =>
        {
            try
            {
                await using var db = await dbContext.CreateDbContextAsync(cancellationToken);
                var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                LogCheckingMigrations(pendingMigrations.Count);

                if (pendingMigrations.Count > 0)
                {
                    await db.Database.MigrateAsync(cancellationToken);
                    LogMigrationsApplied();
                }
                else
                {
                    LogUpToDate();
                }
            }
            catch (Exception ex)
            {
                LogRunFailed(ex);
                throw;
            }
        });

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking database migrations; {PendingCount} pending")]
    private partial void LogCheckingMigrations(int pendingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private partial void LogMigrationsApplied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database already up to date")]
    private partial void LogUpToDate();

    [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed")]
    private partial void LogRunFailed(Exception exception);
}
