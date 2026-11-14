namespace NexGrades.Domain.Grading;

/// <summary>The averages of one student on the grade sheet, as computed by <see cref="GradeSheetAverages"/>.</summary>
/// <param name="BucketAverages">The average per bucket id; null for a bucket without counting grades.</param>
/// <param name="SubjectAverage">The weighted subject average, or null when no bucket has counting grades.</param>
public sealed record GradeSheetStudentAverages(
    IReadOnlyDictionary<int, decimal?> BucketAverages,
    decimal? SubjectAverage)
{
    /// <summary>
    /// The final grade proposed from <see cref="SubjectAverage"/> (see <see cref="GradingScale.ProposeFinalGrade"/>),
    /// or null when there is no subject average. Recomputed from the averages, so it follows live edits.
    /// </summary>
    public decimal? ProposedFinalGrade => GradingScale.ProposeFinalGrade(SubjectAverage);
}
