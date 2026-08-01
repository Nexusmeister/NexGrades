using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");

        // Only blocks exact duplicate (StudentId, ClassInstanceId) rows. "At most one enrollment per school
        // year" cannot be expressed as a plain unique constraint without a denormalized SchoolYearId column
        // here, which the model deliberately omits — see the remarks on Enrollment. Enforced in the domain
        // layer (EnrollmentInvariants) instead.
        builder.HasIndex(e => new { e.StudentId, e.ClassInstanceId }).IsUnique();

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ClassInstance)
            .WithMany(ci => ci.Enrollments)
            .HasForeignKey(e => e.ClassInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
