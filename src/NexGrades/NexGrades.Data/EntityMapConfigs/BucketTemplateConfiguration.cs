using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class BucketTemplateConfiguration : IEntityTypeConfiguration<BucketTemplate>
{
    public void Configure(EntityTypeBuilder<BucketTemplate> builder)
    {
        builder.ToTable("BucketTemplates");

        builder.Property(e => e.Name).IsRequired();

        // Decimal weight maps to TEXT, not REAL, to avoid float drift in weighted averages.
        builder.Property(e => e.Weight).HasColumnType("TEXT");

        builder.HasOne(e => e.Subject)
            .WithMany(s => s.BucketTemplates)
            .HasForeignKey(e => e.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
