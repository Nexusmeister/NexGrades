namespace NexGrades.Domain.Models;

public class SchoolYear
{
    public required string Name { get; set; }
    public DateOnly YearStart { get; set; }
    public DateOnly YearEnd { get; set; }

    public IList<Class> Classes { get; set; } = [];
}