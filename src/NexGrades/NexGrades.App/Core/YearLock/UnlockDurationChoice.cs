namespace NexGrades.App.Core.YearLock;

/// <summary>One entry of the duration list in the unlock dialog.</summary>
/// <param name="Option">The duration the entry stands for.</param>
/// <param name="Label">The text shown for it.</param>
public sealed record UnlockDurationChoice(UnlockDurationOption Option, string Label);
