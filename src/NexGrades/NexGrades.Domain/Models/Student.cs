namespace NexGrades.Domain.Models;

public record Student
{
    public required string Name { get; set; }
    public required string FirstName { get; set; }

    public Class Class { get; set; }
    public IList<Grade> Grades { get; set; } = [];
}