using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Note"/> to the Notes table, including its links to student, school year and class.</summary>
public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Note"/>.</summary>
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("Notes");

        builder.Property(e => e.Text).IsRequired();

        builder.Property(e => e.ArchivedOn);

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
