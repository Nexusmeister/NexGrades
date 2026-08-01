using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class SubjectParticipationConfiguration : IEntityTypeConfiguration<SubjectParticipation>
{
    public void Configure(EntityTypeBuilder<SubjectParticipation> builder)
    {
        builder.ToTable("SubjectParticipations");

        builder.Property(e => e.Mode).HasConversion<string>();

        builder.HasOne(e => e.SubjectInstance)
            .WithMany(si => si.SubjectParticipations)
            .HasForeignKey(e => e.SubjectInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Student)
            .WithMany(s => s.SubjectParticipations)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
