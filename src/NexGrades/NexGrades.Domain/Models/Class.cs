namespace NexGrades.Domain.Models;

public record Class
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public IList<Student> Students { get; set; } = [];
    public IList<Subject> Subjects { get; set; }
}