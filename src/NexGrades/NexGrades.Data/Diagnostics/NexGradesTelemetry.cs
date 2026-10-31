using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NexGrades.Data.Diagnostics;

/// <summary>
/// The app's single <see cref="ActivitySource"/> and <see cref="Meter"/>, both named <see cref="Name"/>, and the
/// instruments on them. It lives in Data because that is the lowest layer every other project can see
/// (Domain and App reference Data), and <see cref="Services.DatabaseMigrationService"/> is instrumented too.
/// </summary>
/// <remarks>
/// <para>
/// <b>Privacy rule: no pupil data in telemetry.</b> Tags may only use the keys below, and their values are
/// operation names, outcomes, page type names and exception type names, never names, grades, note text or
/// other record content. An id is acceptable; the current code does not need one. Exception messages are not
/// recorded either, because they can name values. See docs/decisions/0002-telemetry-and-privacy.md.
/// </para>
/// <para>
/// Nothing here exports anything. Whether the data leaves the process is decided by the service defaults
/// (only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set).
/// </para>
/// </remarks>
public static class NexGradesTelemetry
{
    /// <summary>Name of the activity source and the meter. Must match the one the service defaults subscribe to.</summary>
    public const string Name = "NexGrades";

    /// <summary>Tag key: the logical operation, e.g. <c>NoteService.AddNote</c>.</summary>
    public const string OperationTag = "nexgrades.operation";

    /// <summary>Tag key: how an operation ended, one of the <see cref="Outcomes"/> values.</summary>
    public const string OutcomeTag = "nexgrades.outcome";

    /// <summary>Tag key: the CLR type name of a navigated page, e.g. <c>StudentsOverviewPage</c>.</summary>
    public const string PageTag = "nexgrades.page";

    /// <summary>Tag key: the CLR type name of the exception that ended an operation (standard semantic convention).</summary>
    public const string ErrorTypeTag = "error.type";

    /// <summary>The complete set of tag keys this app is allowed to put on spans and metrics.</summary>
    public static readonly IReadOnlySet<string> AllowedTagKeys =
        new HashSet<string>(StringComparer.Ordinal) { OperationTag, OutcomeTag, PageTag, ErrorTypeTag };

    /// <summary>The app's activity source (traces).</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    /// <summary>The app's meter (metrics).</summary>
    public static readonly Meter Meter = new(Name);

    /// <summary>Time from process start until the main window is shown.</summary>
    public static readonly Histogram<double> StartupDuration = Meter.CreateHistogram<double>(
        "nexgrades.startup.duration", unit: "s", description: "Time from process start until the main window is shown.");

    /// <summary>Time to navigate to a page (resolve and show it), tagged with the page type.</summary>
    public static readonly Histogram<double> NavigationDuration = Meter.CreateHistogram<double>(
        "nexgrades.navigation.duration", unit: "s", description: "Duration of navigating to a page.");

    /// <summary>Duration of an instrumented operation (Domain services, the startup migration), tagged with the operation and its outcome.</summary>
    public static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "nexgrades.operation.duration", unit: "s", description: "Duration of a Domain service operation.");

    /// <summary>The values of <see cref="OutcomeTag"/>.</summary>
    public static class Outcomes
    {
        /// <summary>The operation completed.</summary>
        public const string Success = "success";

        /// <summary>A business rule rejected the operation.</summary>
        public const string InvariantViolation = "invariant_violation";

        /// <summary>A write to a closed school year was rejected.</summary>
        public const string ClosedYear = "closed_year";

        /// <summary>The caller cancelled the operation.</summary>
        public const string Cancelled = "cancelled";

        /// <summary>The operation failed unexpectedly.</summary>
        public const string Error = "error";
    }

    /// <summary>
    /// Runs <paramref name="body"/> inside a span named <paramref name="operation"/> and records its duration and
    /// outcome on <see cref="OperationDuration"/>. Failures are rethrown unchanged.
    /// </summary>
    /// <param name="operation">A fixed operation name, never built from record content.</param>
    /// <param name="body">The work to measure.</param>
    /// <param name="classifyFailure">
    /// Maps a failure to an outcome; the default distinguishes cancellation from errors. Domain passes a mapper that
    /// also knows its own exception types.
    /// </param>
    public static async Task<T> TrackAsync<T>(string operation, Func<Task<T>> body, Func<Exception, string>? classifyFailure = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        using var activity = ActivitySource.StartActivity(operation);
        var start = Stopwatch.GetTimestamp();
        var outcome = Outcomes.Success;
        try
        {
            return await body();
        }
        catch (Exception ex)
        {
            outcome = (classifyFailure ?? ClassifyDefault)(ex);
            MarkFailed(activity, ex);
            throw;
        }
        finally
        {
            activity?.SetTag(OutcomeTag, outcome);
            OperationDuration.Record(
                Stopwatch.GetElapsedTime(start).TotalSeconds,
                new KeyValuePair<string, object?>(OperationTag, operation),
                new KeyValuePair<string, object?>(OutcomeTag, outcome));
        }
    }

    /// <inheritdoc cref="TrackAsync{T}(string, Func{Task{T}}, Func{Exception, string}?)"/>
    public static Task TrackAsync(string operation, Func<Task> body, Func<Exception, string>? classifyFailure = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        return TrackAsync(operation, async () =>
        {
            await body();
            return true;
        }, classifyFailure);
    }

    /// <summary>
    /// Marks <paramref name="activity"/> as failed. Only the exception type is recorded: messages can contain
    /// record content (see the class remarks), and stack traces are not needed to see that something failed.
    /// </summary>
    public static void MarkFailed(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        var type = exception.GetType().Name;
        _ = activity.SetStatus(ActivityStatusCode.Error, type);
        _ = activity.SetTag(ErrorTypeTag, type);
    }

    private static string ClassifyDefault(Exception exception) =>
        exception is OperationCanceledException ? Outcomes.Cancelled : Outcomes.Error;
}
