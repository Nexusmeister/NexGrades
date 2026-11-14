using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="FinalGrade"/> to the FinalGrades table.</summary>
public class FinalGradeConfiguration : IEntityTypeConfiguration<FinalGrade>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="FinalGrade"/>.</summary>
    public void Configure(EntityTypeBuilder<FinalGrade> builder)
    {
        builder.ToTable("FinalGrades");

        // Decimal value maps to TEXT, not REAL, like Grade.Value.
        builder.Property(e => e.OverrideValue).HasColumnType("TEXT");

        // One row per student and subject instance; clearing an override keeps the row with a null value.
        builder.HasIndex(e => new { e.SubjectInstanceId, e.StudentId })
            .IsUnique();

        builder.HasOne(e => e.SubjectInstance)
            .WithMany()
            .HasForeignKey(e => e.SubjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
