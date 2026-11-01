using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="ClassInstance"/> to the ClassInstances table.</summary>
public class ClassInstanceConfiguration : IEntityTypeConfiguration<ClassInstance>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="ClassInstance"/>.</summary>
    public void Configure(EntityTypeBuilder<ClassInstance> builder)
    {
        builder.ToTable("ClassInstances");

        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>();

        builder.HasIndex(e => new { e.ClassGroupId, e.SchoolYearId }).IsUnique();

        builder.HasOne(e => e.ClassGroup)
            .WithMany(g => g.ClassInstances)
            .HasForeignKey(e => e.ClassGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SchoolYear)
            .WithMany(sy => sy.ClassInstances)
            .HasForeignKey(e => e.SchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Teacher)
            .WithMany(t => t.ClassInstances)
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
