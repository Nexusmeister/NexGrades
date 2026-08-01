using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
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
