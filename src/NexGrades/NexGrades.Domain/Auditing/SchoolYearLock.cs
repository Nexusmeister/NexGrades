using NexGrades.Data.Entities;

namespace NexGrades.Domain.Auditing;

/// <summary>
/// The single definition of "can this school year be written to right now", shared by
/// <see cref="ClosedYearAuditInterceptor"/> (which enforces it) and by services and UI that want to tell the
/// user in advance, so the two can never disagree.
/// </summary>
public static class SchoolYearLock
{
    /// <summary>
    /// True unless the year is <see cref="SchoolYearStatus.Closed"/> and not currently unlocked. A year counts
    /// as unlocked only while <see cref="SchoolYear.UnlockedUntil"/> is strictly later than <paramref name="now"/>.
    /// </summary>
    public static bool IsWritable(SchoolYear schoolYear, DateTime now) =>
        IsWritable(schoolYear.Status, schoolYear.UnlockedUntil, now);

    /// <summary>
    /// Same rule as <see cref="IsWritable(SchoolYear, DateTime)"/> for callers that hold only the lock fields,
    /// such as <see cref="Services.SchoolYearLockState"/>.
    /// </summary>
    public static bool IsWritable(SchoolYearStatus status, DateTime? unlockedUntil, DateTime now) =>
        status != SchoolYearStatus.Closed
        || (unlockedUntil is { } until && until > now);
}
