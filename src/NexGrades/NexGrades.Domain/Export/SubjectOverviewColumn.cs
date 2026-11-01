namespace NexGrades.Domain.Export;

/// <summary>
/// Describes one column of a <see cref="SubjectOverviewTable"/>. Carries no header text: the Domain stays free of
/// UI wording, and the writer builds localizable headers from the kind and the bucket name.
/// </summary>
/// <param name="Kind">What the column holds.</param>
/// <param name="BucketName">The bucket's name for the bucket columns; <see langword="null"/> otherwise.</param>
public sealed record SubjectOverviewColumn(SubjectOverviewColumnKind Kind, string? BucketName);
