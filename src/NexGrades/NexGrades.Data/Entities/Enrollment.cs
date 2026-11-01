namespace NexGrades.Data.Entities;

/// <summary>
/// Student &lt;-&gt; class instance membership for one school year.
/// </summary>
/// <remarks>
/// Invariant: a student has at most one enrollment per school year. That rule spans every
/// <see cref="ClassInstance"/> belonging to the same <see cref="Entities.SchoolYear"/>, which cannot be
/// expressed as a plain database unique constraint without a denormalized SchoolYearId column here — and the
/// model deliberately omits that column (year is derived by traversal everywhere except SchoolYear, Note, and
/// ClassInstance). The schema only prevents exact duplicate (StudentId, ClassInstanceId) rows via a unique
/// index; "at most one per school year" is enforced by <c>EnrollmentInvariants</c> in the domain layer, inside
/// a transaction that resolves the candidate ClassInstance -> SchoolYear and checks for existing enrollments
/// across every ClassInstance belonging to that same SchoolYear.
/// </remarks>
public class Enrollment
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>The enrolled student.</summary>
    public int StudentId { get; set; }

    /// <summary>Navigation property for <see cref="StudentId"/>.</summary>
    public Student Student { get; set; } = null!;

    /// <summary>The class the student is enrolled in.</summary>
    public int ClassInstanceId { get; set; }

    /// <summary>Navigation property for <see cref="ClassInstanceId"/>.</summary>
    public ClassInstance ClassInstance { get; set; } = null!;

    /// <summary>First day of the membership. The enrollment is active from this date on, inclusive.</summary>
    public DateTime JoinedOn { get; set; }

    /// <summary>The day the student left. The enrollment is no longer active from this date on; null while the student is still enrolled.</summary>
    public DateTime? LeftOn { get; set; }
}
