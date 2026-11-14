using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;

namespace NexGrades.Domain.Services;

/// <summary>A school year's lock fields as the UI shows them, without the tracked entity.</summary>
/// <param name="SchoolYearId">The school year id.</param>
/// <param name="Label">The display label, e.g. "2025/26".</param>
/// <param name="Status">Where the year is in its Planned, Active, Closed lifecycle.</param>
/// <param name="ClosedOn">When the year was closed, or null while it is not closed.</param>
/// <param name="UnlockedUntil">When the temporary unlock ends, or null when the year is not unlocked.</param>
/// <param name="UnlockReason">Why the year was unlocked, or null when it is not unlocked.</param>
public sealed record SchoolYearLockState(
    int SchoolYearId,
    string Label,
    SchoolYearStatus Status,
    DateTime? ClosedOn,
    DateTime? UnlockedUntil,
    string? UnlockReason)
{
    /// <summary>True when the year is <see cref="SchoolYearStatus.Closed"/>, whether or not it is unlocked.</summary>
    public bool IsClosed => Status == SchoolYearStatus.Closed;

    /// <summary>
    /// True when writes are allowed at <paramref name="now"/>; the same rule the audit interceptor enforces,
    /// see <see cref="SchoolYearLock.IsWritable(SchoolYearStatus, DateTime?, DateTime)"/>.
    /// </summary>
    public bool IsWritableAt(DateTime now) => SchoolYearLock.IsWritable(Status, UnlockedUntil, now);
}
