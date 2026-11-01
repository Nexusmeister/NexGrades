using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Student"/> to the Students table.</summary>
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Student"/>.</summary>
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>();
    }
}
