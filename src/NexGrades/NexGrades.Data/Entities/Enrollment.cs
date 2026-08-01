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
    public int Id { get; set; }

    public int StudentId { get; set; }

    public Student Student { get; set; } = null!;

    public int ClassInstanceId { get; set; }

    public ClassInstance ClassInstance { get; set; } = null!;

    public DateTime JoinedOn { get; set; }

    public DateTime? LeftOn { get; set; }
}
