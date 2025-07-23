namespace NexGrades.Domain.Models;

public class Subject
{
    public required string Name { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is Subject sub)
        {
            return Name.Equals(sub.Name);
        }
        else
        {
            return false;
        }
    }
}