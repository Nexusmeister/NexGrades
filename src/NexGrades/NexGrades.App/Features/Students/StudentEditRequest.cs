namespace NexGrades.App.Features.Students;

/// <summary>
/// Hands the id of the student to edit from the overview to the (transient) <see cref="StudentViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class StudentEditRequest
{
    public int? StudentId { get; set; }
}
