using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class StudentConfiguration : IEntityTypeConfiguration<StudentEntity>
{
    public void Configure(EntityTypeBuilder<StudentEntity> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(k => k.Id)
            .HasName("PK_Students");

        builder.Property(p => p.Id)
            .HasColumnName("Id")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode();

        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(50)
            .IsUnicode();
    }
}