using System.Text;

namespace NexGrades.App.Logging;

/// <summary>
/// Turns an exception into log text. Messages of Domain exceptions are replaced by a placeholder because they name
/// ids and values of pupil records; the type, stack trace and inner chain are kept, which is what diagnosis needs.
/// Messages of framework and third-party exceptions are kept as they are.
/// </summary>
public static class ExceptionDescriber
{
    /// <summary>The text that stands in for the message of a Domain exception.</summary>
    public const string RedactedMessage = "[message redacted]";

    private const string DomainNamespacePrefix = "NexGrades.Domain";
    private const int MaxDepth = 16;

    /// <summary>Describes the exception, its stack trace and its inner exceptions.</summary>
    /// <param name="exception">The exception to describe.</param>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var builder = new StringBuilder();
        Append(builder, exception, 0);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, Exception exception, int depth)
    {
        _ = builder.Append(exception.GetType().FullName).Append(": ").Append(IsRedacted(exception, 0) ? RedactedMessage : exception.Message);
        if (exception.StackTrace is { } stackTrace)
        {
            _ = builder.AppendLine().Append(stackTrace);
        }

        if (depth >= MaxDepth)
        {
            return;
        }

        // AggregateException.InnerException is only the first of InnerExceptions, so walk the full list there.
        IEnumerable<Exception> inner = exception is AggregateException aggregate
            ? aggregate.InnerExceptions
            : exception.InnerException is { } single ? [single] : [];
        foreach (var child in inner)
        {
            _ = builder.AppendLine().Append("--- Inner exception ---").AppendLine();
            Append(builder, child, depth + 1);
        }
    }

    /// <summary>
    /// A Domain exception is redacted, and so is an aggregate that contains one, because the aggregate's own
    /// message embeds the messages of its inner exceptions.
    /// </summary>
    private static bool IsRedacted(Exception exception, int depth)
    {
        if (exception.GetType().Namespace is { } ns && ns.StartsWith(DomainNamespacePrefix, StringComparison.Ordinal))
        {
            return true;
        }

        return depth < MaxDepth
            && exception is AggregateException aggregate
            && aggregate.InnerExceptions.Any(e => IsRedacted(e, depth + 1));
    }
}
