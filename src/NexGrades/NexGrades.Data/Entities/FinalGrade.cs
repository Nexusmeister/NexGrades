namespace NexGrades.Data.Entities;

/// <summary>
/// The teacher's override of a student's final (certificate) grade in one subject instance. The school year is
/// implicit via FinalGrade -> SubjectInstance -> ClassInstance -> SchoolYear; there is deliberately no year column.
/// </summary>
/// <remarks>
/// Only the override is stored. The proposed final grade is always derived from the weighted subject average,
/// which uses the snapshotted <see cref="GradeBucket.Weight"/>, so storing it would only let it drift from the
/// grades. A row exists only once the teacher has set an override for the student. Clearing the override sets
/// <see cref="OverrideValue"/> to <see langword="null"/> rather than removing the row, because nothing is ever
/// deleted; a row with no override means "use the proposal". At most one row exists per subject instance and
/// student.
/// </remarks>
public class FinalGrade
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The subject instance the final grade is given in.</summary>
    public int SubjectInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="SubjectInstanceId"/>.</summary>
    public SubjectInstance SubjectInstance { get; set; } = null!;

    /// <summary>The student who receives the final grade.</summary>
    public int StudentId { get; set; }

    /// <summary>Navigation property for <see cref="StudentId"/>.</summary>
    public Student Student { get; set; } = null!;

    /// <summary>
    /// The whole grade (1 to 6) the teacher set instead of the proposal, stored as <c>decimal</c> like
    /// <see cref="Grade.Value"/>, or <see langword="null"/> when the override was cleared and the proposal applies.
    /// Legal values are defined by <c>GradingScale</c>.
    /// </summary>
    public decimal? OverrideValue { get; set; }
}
