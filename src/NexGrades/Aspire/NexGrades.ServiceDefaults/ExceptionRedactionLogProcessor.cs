using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NexGrades.ServiceDefaults;

/// <summary>
/// Strips exception messages and stack traces from log records before they reach an exporter, keeping only the
/// exception type name. Exception messages in this app name record ids and school years (for example
/// <c>ClosedYearException</c>), and the OpenTelemetry log exporter would otherwise send
/// <c>exception.message</c> and <c>exception.stacktrace</c> for every <c>ILogger</c> call that carries an exception,
/// including calls made by the framework (EF Core, hosting). See docs/decisions/0002-telemetry-and-privacy.md.
/// </summary>
/// <remarks>
/// It works in <see cref="OnEnd"/>, which runs when the record is emitted and before any export processor copies
/// or batches it. It must be registered before the exporter; <see cref="ServiceDefaultsExtensions"/> adds it in the
/// same pipeline ahead of <c>UseOtlpExporter</c>. Processors of one pipeline run in registration order.
/// </remarks>
public sealed class ExceptionRedactionLogProcessor : BaseProcessor<LogRecord>
{
    /// <summary>Attribute key of the exception type name (standard semantic convention).</summary>
    public const string ExceptionTypeKey = "exception.type";

    /// <inheritdoc />
    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var exception = data.Exception;
        if (exception is null)
        {
            return;
        }

        var attributes = new List<KeyValuePair<string, object?>>();
        if (data.Attributes is { } existing)
        {
            attributes.AddRange(existing);
        }

        attributes.Add(new KeyValuePair<string, object?>(ExceptionTypeKey, exception.GetType().Name));

        data.Attributes = attributes;
        data.Exception = null;
    }
}
