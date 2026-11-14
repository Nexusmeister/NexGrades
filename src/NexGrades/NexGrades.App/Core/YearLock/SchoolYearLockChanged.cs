using NexGrades.Domain.Services;

namespace NexGrades.App.Core.YearLock;

/// <summary>Message that <see cref="SchoolYearLockMonitor"/> broadcasts when it learns a school year's lock state.</summary>
/// <param name="State">The lock state as it was just read or derived.</param>
internal sealed record SchoolYearLockChanged(SchoolYearLockState State);
