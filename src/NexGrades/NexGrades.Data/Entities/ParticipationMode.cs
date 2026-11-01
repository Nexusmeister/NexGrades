namespace NexGrades.Data.Entities;

/// <summary>
/// A per-student override of the default rule that enrollment in a class means participation in its subjects.
/// </summary>
public enum ParticipationMode
{
    /// <summary>The student takes part in the subject instance, as an explicit override of the enrollment default.</summary>
    Participating,

    /// <summary>The student does not take part. Grades cannot be recorded for them in this subject instance.</summary>
    Exempt,
}
