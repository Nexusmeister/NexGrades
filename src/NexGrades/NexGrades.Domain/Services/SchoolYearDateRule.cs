using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Shared rule that a date entered for an assessment or grade lies within its school year, so that
/// <see cref="AssessmentService"/> and <see cref="GradeService"/> cannot disagree about it.
/// </summary>
internal static class SchoolYearDateRule
{
    /// <summary>Throws unless <paramref name="date"/> is within <paramref name="schoolYear"/>'s start and end date, inclusive.</summary>
    /// <exception cref="InvariantViolationException">The date is outside the school year.</exception>
    public static void EnsureDateInYear(DateOnly date, SchoolYear schoolYear)
    {
        var asDateTime = date.ToDateTime(TimeOnly.MinValue);
        if (asDateTime < schoolYear.StartDate.Date || asDateTime > schoolYear.EndDate.Date)
        {
            throw new InvariantViolationException(
                $"{date:O} is outside school year {schoolYear.Id} ({DateOnly.FromDateTime(schoolYear.StartDate):O} to {DateOnly.FromDateTime(schoolYear.EndDate):O}).");
        }
    }
}
