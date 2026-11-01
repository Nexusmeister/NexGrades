namespace NexGrades.App.Features.Classes;

/// <summary>
/// Hands the id of the class to edit from the overview to the (transient) <see cref="ClassViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class ClassEditRequest
{
    public int? ClassInstanceId { get; set; }
}
