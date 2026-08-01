namespace NexGrades.Domain.Exceptions;

/// <summary>
/// Thrown when a write would touch an entity reachable from a <c>Closed</c> <see cref="Data.Entities.SchoolYear"/>
/// that is not currently unlocked. A subtype of <see cref="InvariantViolationException"/> so callers that only
/// care about "was this write rejected for domain reasons" can catch the base type, while callers that
/// specifically care about the locked-year case can catch this one.
/// </summary>
public sealed class ClosedYearException : InvariantViolationException
{
    public ClosedYearException(string message)
        : base(message)
    {
    }
}
