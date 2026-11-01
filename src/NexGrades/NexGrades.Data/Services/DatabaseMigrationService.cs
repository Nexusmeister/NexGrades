using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Entities;

namespace NexGrades.Data.Services;

/// <summary>
/// Brings the database up to date at startup: applies pending EF Core migrations, then makes sure an Active school
/// year exists. The App runs it before any screen opens, so the rest of the app can assume a schema and a year.
/// </summary>
public partial class DatabaseMigrationService(IDbContextFactory<AppDbContext> dbContext, ILogger<DatabaseMigrationService> logger)
{
    /// <summary>
    /// Applies pending migrations and bootstraps an Active school year if none exists. Tracked as
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

                await EnsureActiveSchoolYearAsync(db, cancellationToken);
            }
            catch (Exception ex)
            {
                LogRunFailed(ex);
                throw;
            }
        });

    /// <summary>
    /// Everything in the model is scoped through a <see cref="SchoolYear"/>, but there is no year-management
    /// UI yet. Rather than block every other screen on that being built first, bootstrap one Active year on
    /// first run so Students/Classes/Subjects keep working. This is a stopgap for a real "start a new school
    /// year" workflow, not a substitute for one.
    /// </summary>
    private async Task EnsureActiveSchoolYearAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var hasActiveYear = await db.SchoolYears.AnyAsync(sy => sy.Status == SchoolYearStatus.Active, cancellationToken);
        if (hasActiveYear)
        {
            return;
        }

        var today = DateTime.Now;
        // German school years run August -> July; if we're before August, the current year started last August.
        var startYear = today.Month >= 8 ? today.Year : today.Year - 1;

        db.SchoolYears.Add(new SchoolYear
        {
            Label = $"{startYear}/{(startYear + 1) % 100:D2}",
            StartDate = new DateTime(startYear, 8, 1),
            EndDate = new DateTime(startYear + 1, 7, 31),
            Status = SchoolYearStatus.Active,
        });

        await db.SaveChangesAsync(cancellationToken);
        LogActiveYearCreated();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Checking database migrations; {PendingCount} pending")]
    private partial void LogCheckingMigrations(int pendingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied")]
    private partial void LogMigrationsApplied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database already up to date")]
    private partial void LogUpToDate();

    [LoggerMessage(Level = LogLevel.Information, Message = "Created an Active school year")]
    private partial void LogActiveYearCreated();

    [LoggerMessage(Level = LogLevel.Error, Message = "Database migration failed")]
    private partial void LogRunFailed(Exception exception);
}
