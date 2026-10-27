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
        schoolYear.Status != SchoolYearStatus.Closed
        || (schoolYear.UnlockedUntil is { } until && until > now);
}
