using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;

namespace NexGrades.Data.Services;

public class DatabaseMigrationService(IDbContextFactory<AppDbContext> dbContext)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var db = await dbContext.CreateDbContextAsync(cancellationToken);
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        if (pendingMigrations.Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        await EnsureActiveSchoolYearAsync(db, cancellationToken);
    }

    /// <summary>
    /// Everything in the model is scoped through a <see cref="SchoolYear"/>, but there is no year-management
    /// UI yet. Rather than block every other screen on that being built first, bootstrap one Active year on
    /// first run so Students/Classes/Subjects keep working. This is a stopgap for a real "start a new school
    /// year" workflow, not a substitute for one.
    /// </summary>
    private static async Task EnsureActiveSchoolYearAsync(AppDbContext db, CancellationToken cancellationToken)
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
    }
}
