namespace NexGrades.App.Core.YearLock;

/// <summary>What the teacher entered in the unlock dialog.</summary>
/// <param name="Until">When the unlock ends.</param>
/// <param name="Reason">Why the year is unlocked.</param>
public sealed record UnlockRequest(DateTime Until, string Reason);
