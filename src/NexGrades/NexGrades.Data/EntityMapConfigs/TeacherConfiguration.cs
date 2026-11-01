using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="Teacher"/> to the Teachers table.</summary>
public class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="Teacher"/>.</summary>
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");

        builder.Property(e => e.DisplayName).IsRequired();
    }
}
