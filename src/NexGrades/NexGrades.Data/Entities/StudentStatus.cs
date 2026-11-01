namespace NexGrades.Data.Entities;

/// <summary>Whether a person still attends the school.</summary>
public enum StudentStatus
{
    /// <summary>The student attends the school.</summary>
    Active,

    /// <summary>The student no longer attends the school. The row and its history are kept.</summary>
    Left,
}
