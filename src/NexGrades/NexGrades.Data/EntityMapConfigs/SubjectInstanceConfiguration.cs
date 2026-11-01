using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="SubjectInstance"/> to the SubjectInstances table.</summary>
public class SubjectInstanceConfiguration : IEntityTypeConfiguration<SubjectInstance>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="SubjectInstance"/>.</summary>
    public void Configure(EntityTypeBuilder<SubjectInstance> builder)
    {
        builder.ToTable("SubjectInstances");

        builder.HasIndex(e => new { e.ClassInstanceId, e.SubjectId }).IsUnique();

        builder.HasOne(e => e.ClassInstance)
            .WithMany(ci => ci.SubjectInstances)
            .HasForeignKey(e => e.ClassInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Subject)
            .WithMany(s => s.SubjectInstances)
            .HasForeignKey(e => e.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Teacher)
            .WithMany(t => t.SubjectInstances)
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
