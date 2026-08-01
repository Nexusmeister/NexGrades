namespace NexGrades.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would violate one of the domain invariants (enrollment uniqueness, grade
/// participation rules, grading scale legality, year-close readiness, etc.) that the database schema itself
/// cannot enforce.
/// </summary>
public class InvariantViolationException : Exception
{
    public InvariantViolationException(string message)
        : base(message)
    {
    }

    public InvariantViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
