namespace NexGrades.Domain.Grading;

/// <summary>One subject of the student's class in the per-student grade list.</summary>
/// <param name="SubjectInstanceId">The subject instance id.</param>
/// <param name="SubjectName">The subject name.</param>
/// <param name="IsExempt">Whether the student is exempt; an exempt student has no averages but keeps the grades listed.</param>
/// <param name="Buckets">The buckets, ordered by sort order and then id.</param>
/// <param name="SubjectAverage">The weighted subject average, or null when there is none (or the student is exempt).</param>
/// <param name="FinalGradeOverride">The final grade the teacher set instead of the proposal, or null when there is none. Always null for an exempt student.</param>
public sealed record StudentSubjectGrades(
    int SubjectInstanceId,
    string SubjectName,
    bool IsExempt,
    IReadOnlyList<StudentBucketGrades> Buckets,
    decimal? SubjectAverage,
    decimal? FinalGradeOverride)
{
    /// <summary>
    /// The final grade proposed from <see cref="SubjectAverage"/>, or null when there is no subject average (always
    /// the case for an exempt student). It stays available next to an override.
    /// </summary>
    public decimal? ProposedFinalGrade => GradingScale.ProposeFinalGrade(SubjectAverage);

    /// <summary>The final grade that applies: the override when one is set, otherwise the proposal.</summary>
    public decimal? FinalGrade => FinalGradeOverride ?? ProposedFinalGrade;
}
