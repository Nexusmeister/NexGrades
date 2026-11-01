using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Subject"/> to the Subjects table.</summary>
public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Subject"/>.</summary>
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ShortCode).IsRequired().HasMaxLength(10);
    }
}
