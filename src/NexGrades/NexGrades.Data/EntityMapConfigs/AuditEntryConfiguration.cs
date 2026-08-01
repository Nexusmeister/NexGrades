using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.Property(e => e.EntityType).IsRequired();
        builder.Property(e => e.Field).IsRequired();

        // Non-unique: supports "show audit history for entity X" queries.
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
