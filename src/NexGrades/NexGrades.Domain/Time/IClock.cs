namespace NexGrades.Domain.Time;

/// <summary>
/// Abstraction over "now", injected everywhere the domain layer needs the current wall-clock time
/// (year close/unlock/rollover timestamps, audit entry timestamps, "is this enrollment still active"
/// checks) so that behaviour is testable without a dependency on the real system clock.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
}
