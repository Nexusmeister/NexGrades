namespace NexGrades.Data.Entities;

/// <summary>
/// A person. Not year-scoped — the same row persists for the student's whole school career.
/// </summary>
public class Student
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Given name, limited to 50 characters.</summary>
    public required string FirstName { get; set; }

    /// <summary>Family name, limited to 50 characters.</summary>
    public required string LastName { get; set; }

    /// <summary>Date of birth, or null when it has not been recorded.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Whether the student still attends the school. Students are changed to <see cref="StudentStatus.Left"/>, never deleted.</summary>
    public StudentStatus Status { get; set; } = StudentStatus.Active;

    /// <summary>The student's class memberships across all school years.</summary>
    public ICollection<Enrollment> Enrollments { get; set; } = [];

    /// <summary>The student's overrides of the default subject participation.</summary>
    public ICollection<SubjectParticipation> SubjectParticipations { get; set; } = [];

    /// <summary>All grades the student has received, across school years.</summary>
    public ICollection<Grade> Grades { get; set; } = [];

    /// <summary>All notes about the student, across school years.</summary>
    public ICollection<Note> Notes { get; set; } = [];
}
