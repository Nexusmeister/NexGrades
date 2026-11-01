using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexGrades.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to create an <see cref="AppDbContext"/> for migrations. It points
/// at a fixed <c>nexgrades.db</c> file, so it never touches the per-user runtime database.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>Creates a context for design-time tooling. <paramref name="args"/> are ignored.</summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=nexgrades.db");

        return new AppDbContext(optionsBuilder.Options);
    }
}
