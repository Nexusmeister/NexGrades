using NexGrades.Data.Entities;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Shared "is this enrollment active as of a given point in time" test, used by <see cref="YearCloseService"/>
/// (to know which students currently participate by default), <see cref="RolloverService"/> (to know which
/// enrollments carry forward), and <see cref="GradeService"/> (to validate a grade against the enrollment
/// window at the time it was recorded). An enrollment is active at a given instant when it has already
/// started (<see cref="Enrollment.JoinedOn"/> is on or before that instant) and has not yet ended (no
/// <see cref="Enrollment.LeftOn"/>, or that date is still after that instant).
/// </summary>
internal static class EnrollmentActivity
{
    /// <summary>Whether <paramref name="enrollment"/> is active right now, per the injected clock.</summary>
    public static bool IsActive(Enrollment enrollment, IClock clock) => IsActive(enrollment, clock.Now);

    /// <summary>Whether <paramref name="enrollment"/> is active as of an arbitrary point in time <paramref name="asOf"/>.</summary>
    public static bool IsActive(Enrollment enrollment, DateTime asOf) => IsActive(enrollment.JoinedOn, enrollment.LeftOn, asOf);

    /// <summary>Same rule for callers that hold only the enrollment's dates, such as a projected read model.</summary>
    public static bool IsActive(DateTime joinedOn, DateTime? leftOn, DateTime asOf) =>
        joinedOn <= asOf && (leftOn is null || asOf < leftOn);

    /// <summary>
    /// Day-based variant for grades and assessments, which carry a date without a time. Enrollment dates may
    /// include a time of day (a student added at 09:15 is on the roll for that whole day), so only their date
    /// part counts. The leave date is exclusive: the student is not active on the day they left.
    /// </summary>
    public static bool IsActiveOn(DateTime joinedOn, DateTime? leftOn, DateOnly date) =>
        DateOnly.FromDateTime(joinedOn) <= date && (leftOn is null || date < DateOnly.FromDateTime(leftOn.Value));
}
