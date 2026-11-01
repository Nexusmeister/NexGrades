using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexGrades.Data.Entities;

namespace NexGrades.Data.EntityMapConfigs;

/// <summary>Maps <see cref="AuditEntry"/> to the AuditEntries table.</summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>Applies the table, column, index and relationship rules for <see cref="AuditEntry"/>.</summary>
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.Property(e => e.EntityType).IsRequired();
        builder.Property(e => e.Field).IsRequired();

        // Non-unique: supports "show audit history for entity X" queries.
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
