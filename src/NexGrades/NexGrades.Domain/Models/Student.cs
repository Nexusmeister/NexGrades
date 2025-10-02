namespace NexGrades.Domain.Models;

public class Student
{
    public required string Name { get; set; }

    public Class Class { get; set; }
    public IList<Grade> Grades { get; set; } = [];
}