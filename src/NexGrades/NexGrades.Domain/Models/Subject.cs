using System.Drawing;

namespace NexGrades.Domain.Models;

public class Subject
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public Color Color { get; set; }

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