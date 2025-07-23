namespace NexGrades.Domain;

public class Grade
{
    public decimal Value { get; set; }
    public uint Multiplier { get; set; }

    public IList<Grade> SubGrades { get; set; }
}