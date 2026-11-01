namespace NexGrades.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would violate one of the domain invariants (enrollment uniqueness, grade
/// participation rules, grading scale legality, year-close readiness, etc.) that the database schema itself
/// cannot enforce.
/// </summary>
public class InvariantViolationException : Exception
{
    /// <summary>Creates the exception with a diagnostic message. The message names the offending ids or values.</summary>
    /// <param name="message">Diagnostic text. It is not shown to the user verbatim.</param>
    public InvariantViolationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception and keeps <paramref name="innerException"/> as its cause.</summary>
    /// <param name="message">Diagnostic text. It is not shown to the user verbatim.</param>
    /// <param name="innerException">The lower-level exception that caused the violation.</param>
    public InvariantViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
