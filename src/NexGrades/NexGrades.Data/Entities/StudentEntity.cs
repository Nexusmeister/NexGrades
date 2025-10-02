namespace NexGrades.Data.Entities;

public record StudentEntity
{
    public int Id { get; init; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}