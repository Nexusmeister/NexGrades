using NexGrades.Data.Diagnostics;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Wraps a Domain service method in a span and a duration/outcome metric (see <see cref="NexGradesTelemetry"/>).
/// </summary>
/// <remarks>
/// Operation names are fixed strings such as <c>NoteService.AddNote</c>. Never pass arguments or record content
/// into them: telemetry must not carry pupil data.
/// </remarks>
internal static class DomainOperation
{
    public static Task<T> RunAsync<T>(string operation, Func<Task<T>> body) =>
        NexGradesTelemetry.TrackAsync(operation, body, Classify);

    public static Task RunAsync(string operation, Func<Task> body) =>
        NexGradesTelemetry.TrackAsync(operation, body, Classify);

    private static string Classify(Exception exception) => exception switch
    {
        ClosedYearException => NexGradesTelemetry.Outcomes.ClosedYear,
        InvariantViolationException => NexGradesTelemetry.Outcomes.InvariantViolation,
        OperationCanceledException => NexGradesTelemetry.Outcomes.Cancelled,
        _ => NexGradesTelemetry.Outcomes.Error,
    };
}
