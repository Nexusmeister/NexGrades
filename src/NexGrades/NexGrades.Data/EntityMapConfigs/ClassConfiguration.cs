using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class ClassConfiguration : IEntityTypeConfiguration<ClassEntity>
{
    public void Configure(EntityTypeBuilder<ClassEntity> builder)
    {
        builder.ToTable("Classes");

        builder.HasKey(pk => pk.Id)
            .HasName("PK_Classes");

        builder.Property(e => e.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.Name)
            .HasColumnName("Name")
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(5);
    }
}