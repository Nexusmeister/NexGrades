using Microsoft.EntityFrameworkCore;
using NexGrades.Data;

namespace NexGrades.App.Infrastructure;

public class DatabaseMigrationService(AppDbContext dbContext)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

        if (pendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}