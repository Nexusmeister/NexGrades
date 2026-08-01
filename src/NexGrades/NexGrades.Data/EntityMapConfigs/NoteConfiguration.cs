using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");

        builder.Property(e => e.Text).IsRequired();

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Notes)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SchoolYear)
            .WithMany(sy => sy.Notes)
            .HasForeignKey(e => e.SchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ClassInstance)
            .WithMany(ci => ci.Notes)
            .HasForeignKey(e => e.ClassInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
