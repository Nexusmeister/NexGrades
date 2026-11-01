namespace NexGrades.App.Features.Classes;

/// <summary>
/// Hands the id of the class to edit from the overview to the (transient) <see cref="ClassViewModel"/>,
/// which is created by the navigation service and therefore can't take it as a constructor argument.
/// </summary>
public sealed class ClassEditRequest
{
    /// <summary>The id of the class to edit, or <see langword="null"/> to add a new class. <c>ClassViewModel</c> clears it once it has read the value.</summary>
    public int? ClassInstanceId { get; set; }
}
