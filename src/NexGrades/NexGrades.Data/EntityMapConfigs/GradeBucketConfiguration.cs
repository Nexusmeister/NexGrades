using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class GradeBucketConfiguration : IEntityTypeConfiguration<GradeBucket>
{
    public void Configure(EntityTypeBuilder<GradeBucket> builder)
    {
        builder.ToTable("GradeBuckets");

        builder.Property(e => e.Name).IsRequired();

        // Decimal weight maps to TEXT, not REAL, to avoid float drift in weighted averages.
        builder.Property(e => e.Weight).HasColumnType("TEXT");

        builder.HasOne(e => e.SubjectInstance)
            .WithMany(si => si.GradeBuckets)
            .HasForeignKey(e => e.SubjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
