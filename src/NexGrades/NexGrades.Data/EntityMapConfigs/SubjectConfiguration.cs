using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class SubjectConfiguration : IEntityTypeConfiguration<SubjectEntity>
{
    public void Configure(EntityTypeBuilder<SubjectEntity> builder)
    {
        builder.HasKey(pk => pk.Id)
            .HasName("PK_Subjects");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(25)
            .IsUnicode();
    }
}