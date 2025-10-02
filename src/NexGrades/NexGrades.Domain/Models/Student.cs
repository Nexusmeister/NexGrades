namespace NexGrades.Domain.Models;

public record Student
{
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;

    public Class Class { get; set; }
    public IList<Grade> Grades { get; set; } = [];
}