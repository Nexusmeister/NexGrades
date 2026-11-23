using NexGrades.Domain.Time;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>Settable <see cref="IClock"/> test double.</summary>
internal sealed class TestClock(DateTime now) : IClock
{
    public DateTime Now { get; set; } = now;
}
