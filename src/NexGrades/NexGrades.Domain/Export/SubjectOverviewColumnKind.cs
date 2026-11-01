namespace NexGrades.Domain.Export;

/// <summary>What a column of a <see cref="SubjectOverviewTable"/> holds.</summary>
public enum SubjectOverviewColumnKind
{
    /// <summary>The student's name, formatted "Last, First".</summary>
    Student,

    /// <summary>The grade tokens of one bucket, joined into a single text cell.</summary>
    BucketGrades,

    /// <summary>The student's average within one bucket.</summary>
    BucketAverage,

    /// <summary>The student's weighted average over the whole subject.</summary>
    SubjectAverage,
}
