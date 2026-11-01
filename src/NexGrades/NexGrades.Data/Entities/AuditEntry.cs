namespace NexGrades.Data.Entities;

/// <summary>
/// Written only for changes made to a closed (temporarily unlocked) school year. Deliberately not a
/// foreign-key relationship: <see cref="EntityType"/> + <see cref="EntityId"/> identify any entity in the
/// model generically, which is exactly what a cross-cutting audit trail needs.
/// </summary>
public class AuditEntry
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The CLR entity name, e.g. "Grade".</summary>
    public required string EntityType { get; set; }

    /// <summary>The Id of the entity that was changed.</summary>
    public int EntityId { get; set; }

    /// <summary>The property/field that was changed.</summary>
    public required string Field { get; set; }

    /// <summary>The previous value as text, or null when the change created the row.</summary>
    public string? OldValue { get; set; }

    /// <summary>The new value as text, formatted with the invariant culture so the audit log reads the same on any machine.</summary>
    public string? NewValue { get; set; }

    /// <summary>When the change was saved.</summary>
    public DateTime ChangedOn { get; set; }

    /// <summary>The reason given when the closed year was unlocked for this change, or null if none was recorded.</summary>
    public string? Reason { get; set; }
}
