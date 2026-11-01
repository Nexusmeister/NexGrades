using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="ClassGroup"/> to the ClassGroups table.</summary>
public class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="ClassGroup"/>.</summary>
    public void Configure(EntityTypeBuilder<ClassGroup> builder)
    {
        builder.ToTable("ClassGroups");

        builder.Property(e => e.CohortLabel).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>();

        builder.HasOne(e => e.FoundedInSchoolYear)
            .WithMany(sy => sy.FoundedClassGroups)
            .HasForeignKey(e => e.FoundedInSchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
