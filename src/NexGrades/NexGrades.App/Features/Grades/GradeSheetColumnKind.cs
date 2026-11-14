namespace NexGrades.App.Features.Grades;

/// <summary>What a <see cref="GradeSheetColumn"/> shows; <see cref="DataGridColumnsBehavior"/> picks the binding from it.</summary>
public enum GradeSheetColumnKind
{
    /// <summary>The frozen student name column.</summary>
    Student,

    /// <summary>One assessment of a bucket; the cells are editable grade tokens.</summary>
    Assessment,

    /// <summary>The read-only list of a bucket's grades that belong to no assessment.</summary>
    OtherGrades,

    /// <summary>The live average of one bucket.</summary>
    BucketAverage,

    /// <summary>The live subject average.</summary>
    SubjectAverage,
}
