namespace NexGrades.Data.Entities;

/// <summary>
/// One graded occasion within a <see cref="Entities.GradeBucket"/>, such as a class test or an oral check, that
/// many students receive a grade for. On the grade sheet it is one column. The school year is implicit via
/// Assessment -> GradeBucket -> SubjectInstance -> ClassInstance -> SchoolYear; there is deliberately no year
/// column here to get out of sync.
/// </summary>
/// <remarks>
/// <see cref="GradeBucketId"/> never changes after creation: every linked <see cref="Grade"/> must count toward
/// the same bucket as its assessment, and the closed-year interceptor resolves an assessment's year through its
/// bucket, so moving an assessment would also move it out from under the lock of the year it came from.
/// Assessments are never deleted; see <see cref="VoidedOn"/>.
/// </remarks>
public class Assessment
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The bucket the assessment's grades count toward. Fixed at creation; see the remarks.</summary>
    public int GradeBucketId { get; set; }

    /// <summary>Navigation property for <see cref="GradeBucketId"/>.</summary>
    public GradeBucket GradeBucket { get; set; } = null!;

    /// <summary>Display title, e.g. "Klassenarbeit 2". Required.</summary>
    public required string Title { get; set; }

    /// <summary>The date the assessment took place. Grades recorded for it take this date by default.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// When the assessment was voided, or <see langword="null"/> while it is active. Set by voiding and cleared
    /// by restoring; this is how a wrongly created assessment is "removed", because nothing is ever deleted.
    /// </summary>
    public DateTime? VoidedOn { get; set; }

    /// <summary>The grades recorded for this assessment, including voided ones.</summary>
    public ICollection<Grade> Grades { get; set; } = [];
}
