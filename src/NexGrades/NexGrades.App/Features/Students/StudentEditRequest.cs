namespace NexGrades.App.Features.Students;

/// <summary>
/// Hands the id of the student to edit from the overview to the (transient) <see cref="StudentViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class StudentEditRequest
{
    /// <summary>The id of the student to edit, or <see langword="null"/> to add a new student. <c>StudentViewModel</c> clears it once it has read the value.</summary>
    public int? StudentId { get; set; }
}
