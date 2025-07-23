namespace NexGrades.Domain;

public class Class
{
    public required string Name { get; set; }

    public IList<Student> Students { get; set; } = [];
    public IList<Subject> Subjects { get; set; }
}