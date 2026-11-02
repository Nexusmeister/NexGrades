namespace NexGrades.Domain.Services;

/// <summary>What a <see cref="CloseReadinessIssue"/> reports. Only <see cref="ClassStillActive"/> blocks the close.</summary>
public enum CloseReadinessIssueKind
{
    /// <summary>A class has been neither rolled forward nor archived. Blocking.</summary>
    ClassStillActive,

    /// <summary>A subject has no grades in any bucket. Advisory.</summary>
    SubjectWithoutGrades,

    /// <summary>The bucket weights of a subject do not add up to 100. Advisory.</summary>
    BucketWeightsNotHundred,

    /// <summary>A participating student has no grade in a subject. Advisory.</summary>
    StudentWithoutGrade,
}
