namespace NexGrades.Data.Entities;

public enum StudentStatus
{
    Active,
    Left,
}

/// <summary>
/// A person. Not year-scoped — the same row persists for the student's whole school career.
/// </summary>
public class Student
{
    public int Id { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public ICollection<Enrollment> Enrollments { get; set; } = [];

    public ICollection<SubjectParticipation> SubjectParticipations { get; set; } = [];

    public ICollection<Grade> Grades { get; set; } = [];

    public ICollection<Note> Notes { get; set; } = [];
}
