using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Grade"/> to the Grades table.</summary>
public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Grade"/>.</summary>
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades");

        // Decimal value maps to TEXT, not REAL, to avoid float drift in weighted averages.
        builder.Property(e => e.Value).HasColumnType("TEXT");

        builder.Property(e => e.VoidedOn);

        // Schema backstop for "one active grade per student per assessment". Partial, so a voided grade can sit
        // next to its replacement and grades without an assessment are not constrained at all.
        builder.HasIndex(e => new { e.AssessmentId, e.StudentId })
            .IsUnique()
            .HasFilter("\"AssessmentId\" IS NOT NULL AND \"VoidedOn\" IS NULL");

        builder.HasOne(e => e.Assessment)
            .WithMany(a => a.Grades)
            .HasForeignKey(e => e.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

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
