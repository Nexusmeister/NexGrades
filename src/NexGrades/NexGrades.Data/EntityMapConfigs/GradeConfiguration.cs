using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades");

        // Decimal value maps to TEXT, not REAL, to avoid float drift in weighted averages.
        builder.Property(e => e.Value).HasColumnType("TEXT");

        builder.HasOne(e => e.GradeBucket)
            .WithMany(gb => gb.Grades)
            .HasForeignKey(e => e.GradeBucketId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Grades)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
