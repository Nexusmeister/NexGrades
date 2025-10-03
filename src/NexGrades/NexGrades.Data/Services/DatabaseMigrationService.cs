using Microsoft.EntityFrameworkCore;

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
    }
}