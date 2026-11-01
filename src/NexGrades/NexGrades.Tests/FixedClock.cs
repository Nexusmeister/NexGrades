using NexGrades.Domain.Time;

namespace NexGrades.Tests;

/// <summary>Settable <see cref="IClock"/> test double, for deterministic time-dependent assertions.</summary>
internal sealed class FixedClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
}
