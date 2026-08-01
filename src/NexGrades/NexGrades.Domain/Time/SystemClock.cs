namespace NexGrades.Domain.Time;

/// <summary>
/// Default <see cref="IClock"/> implementation backed by the real system clock.
/// </summary>
/// <remarks>
/// Deliberately local time (<see cref="DateTime.Now"/>), not UTC: every <see cref="DateTime"/> in the entity
/// model is local, not <see cref="DateTimeOffset"/>, so switching only this call site to UTC would silently
/// create a mismatch against the rest of the system — don't "fix" this in isolation.
/// </remarks>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
}
