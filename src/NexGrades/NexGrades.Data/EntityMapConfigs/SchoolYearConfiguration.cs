using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="SchoolYear"/> to the SchoolYears table.</summary>
public class SchoolYearConfiguration : IEntityTypeConfiguration<SchoolYear>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="SchoolYear"/>.</summary>
    public void Configure(EntityTypeBuilder<SchoolYear> builder)
    {
        builder.ToTable("SchoolYears");

        builder.Property(e => e.Label).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>();
    }
}
