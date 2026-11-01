using System.Globalization;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;

namespace NexGrades.App.Logging;

/// <summary>
/// Writes one log event as <c>2026-10-31 14:03:07.123 +01:00 [INF] Category Message</c>, followed by the exception
/// text from <see cref="ExceptionDescriber"/> when there is one. The invariant culture keeps the files readable and
/// comparable no matter which UI language is active. Rendering the exception here, instead of with Serilog's
/// default, is what applies the redaction of Domain exception messages.
/// </summary>
public sealed class LogFileFormatter : ITextFormatter
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    /// <summary>Formats the event onto the output writer.</summary>
    /// <param name="logEvent">The event to write.</param>
    /// <param name="output">The destination writer.</param>
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(logEvent.Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        output.Write(" [");
        output.Write(LevelCode(logEvent.Level));
        output.Write("] ");

        if (logEvent.Properties.TryGetValue("SourceContext", out var context) && context is ScalarValue { Value: string category })
        {
            output.Write(category);
            output.Write(' ');
        }

        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            if (token is PropertyToken property && logEvent.Properties.TryGetValue(property.PropertyName, out var value))
            {
                // "l" writes strings without quotes, like the :lj output-template format. It is only valid for
                // strings; numeric and other values throw FormatException with it.
                var format = value is ScalarValue { Value: string } ? "l" : null;
                value.Render(output, format, CultureInfo.InvariantCulture);
            }
            else
            {
                output.Write(token.ToString());
            }
        }

        output.WriteLine();
        if (logEvent.Exception is not null)
        {
            output.WriteLine(ExceptionDescriber.Describe(logEvent.Exception));
        }
    }

    private static string LevelCode(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "VRB",
        LogEventLevel.Debug => "DBG",
        LogEventLevel.Information => "INF",
        LogEventLevel.Warning => "WRN",
        LogEventLevel.Error => "ERR",
        LogEventLevel.Fatal => "FTL",
        _ => "???",
    };
}
