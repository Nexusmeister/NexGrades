using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Data.EntityMapConfigs;

namespace NexGrades.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StudentEntity> Students { get; set; }
    public DbSet<ClassEntity> Classes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new ClassConfiguration());
    }
}