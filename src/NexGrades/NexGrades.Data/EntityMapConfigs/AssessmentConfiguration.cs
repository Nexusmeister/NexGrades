using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Assessment"/> to the Assessments table.</summary>
public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Assessment"/>.</summary>
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessments");

        builder.Property(e => e.Title).IsRequired();

        builder.Property(e => e.VoidedOn);

        // The grade sheet lists a bucket's assessments in date order.
        builder.HasIndex(e => new { e.GradeBucketId, e.Date });

        builder.HasOne(e => e.GradeBucket)
            .WithMany(gb => gb.Assessments)
            .HasForeignKey(e => e.GradeBucketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
