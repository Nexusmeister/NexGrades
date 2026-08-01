namespace NexGrades.Data.Entities;

/// <summary>
/// Written only for changes made to a closed (temporarily unlocked) school year. Deliberately not a
/// foreign-key relationship: <see cref="EntityType"/> + <see cref="EntityId"/> identify any entity in the
/// model generically, which is exactly what a cross-cutting audit trail needs.
/// </summary>
public class AuditEntry
{
    public int Id { get; set; }

    /// <summary>The CLR entity name, e.g. "Grade".</summary>
    public required string EntityType { get; set; }

    /// <summary>The Id of the entity that was changed.</summary>
    public int EntityId { get; set; }

    /// <summary>The property/field that was changed.</summary>
    public required string Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime ChangedOn { get; set; }

    public string? Reason { get; set; }
}
