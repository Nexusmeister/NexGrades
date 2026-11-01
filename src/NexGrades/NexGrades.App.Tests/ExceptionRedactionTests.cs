using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.ServiceDefaults;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards the privacy rule that exception messages and stack traces never leave the process in exported logs:
/// first the processor on its own, then the real wiring against the real OTLP exporter.
/// </summary>
[Collection(SharedTelemetryState.Name)]
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Tests log once to drive the pipeline; performance is irrelevant.")]
public class ExceptionRedactionTests
{
    private const string SecretMessage = "ZZ-SECRET id 42 2025/26";

    /// <summary>What an exporter placed after the redactor would see of one record.</summary>
    private sealed record Captured(Exception? Exception, Dictionary<string, object?> Attributes);

    /// <summary>Added after the redactor, so it observes the record as the exporter would.</summary>
    private sealed class CapturingProcessor : BaseProcessor<LogRecord>
    {
        public List<Captured> Records { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            var attributes = new Dictionary<string, object?>();
            if (data.Attributes is { } existing)
            {
                foreach (var pair in existing)
                {
                    attributes[pair.Key] = pair.Value;
                }
            }

            Records.Add(new Captured(data.Exception, attributes));
        }
    }

    private static CapturingProcessor LogThroughRedactor(Action<ILogger> log)
    {
        var capture = new CapturingProcessor();
        using (var factory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.AddProcessor(new ExceptionRedactionLogProcessor());
            options.AddProcessor(capture);
        })))
        {
            log(factory.CreateLogger("Test"));
        }

        return capture;
    }

    // ---------- Processor ----------

    [Fact]
    public void OnEnd_RemovesTheException_WhenTheRecordCarriesOne()
    {
        var capture = LogThroughRedactor(logger =>
            logger.LogError(new InvalidOperationException(SecretMessage), "Operation failed"));

        var record = Assert.Single(capture.Records);
        Assert.Null(record.Exception);
    }

    [Fact]
    public void OnEnd_AddsTheExceptionTypeName_WhenTheRecordCarriesAnException()
    {
        var capture = LogThroughRedactor(logger =>
            logger.LogError(new InvalidOperationException(SecretMessage), "Operation failed"));

        var record = Assert.Single(capture.Records);
        Assert.Equal(nameof(InvalidOperationException), record.Attributes[ExceptionRedactionLogProcessor.ExceptionTypeKey]);
    }

    [Fact]
    public void OnEnd_LeavesNoAttributeContainingTheMessage_WhenTheRecordCarriesAnException()
    {
        var capture = LogThroughRedactor(logger =>
            logger.LogError(new InvalidOperationException(SecretMessage), "Operation failed"));

        var record = Assert.Single(capture.Records);
        Assert.DoesNotContain(record.Attributes, a =>
            a.Key.Contains("ZZ-SECRET", StringComparison.Ordinal)
            || Convert.ToString(a.Value, CultureInfo.InvariantCulture)!
                .Contains("ZZ-SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public void OnEnd_KeepsExistingAttributes_WhenTheRecordCarriesAnException()
    {
        var capture = LogThroughRedactor(logger =>
            logger.LogError(new InvalidOperationException(SecretMessage), "Failed {Operation}", "AddNote"));

        var record = Assert.Single(capture.Records);
        Assert.Equal("AddNote", record.Attributes["Operation"]);
    }

    [Fact]
    public void OnEnd_LeavesTheRecordUnchanged_WhenThereIsNoException()
    {
        var capture = LogThroughRedactor(logger => logger.LogError("Failed {Operation}", "AddNote"));

        var record = Assert.Single(capture.Records);
        Assert.Null(record.Exception);
        Assert.Equal("AddNote", record.Attributes["Operation"]);
        Assert.DoesNotContain(ExceptionRedactionLogProcessor.ExceptionTypeKey, record.Attributes.Keys);
    }

    // ---------- Wiring against the real exporter ----------

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Task ServeAsync(HttpListener listener, List<byte[]> bodies, CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);
                    using var buffer = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    lock (bodies)
                    {
                        bodies.Add(buffer.ToArray());
                    }

                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
            {
                // The listener was stopped: nothing left to serve.
            }
        }, cancellationToken);

    private static async Task LogErrorThroughRealWiringAsync(string url, CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ServiceDefaultsExtensions.OtlpEndpointKey] = url,
            ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
        });
        builder.AddServiceDefaults("Test");

        using var host = builder.Build();
        await host.StartAsync(cancellationToken);
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Test")
            .LogError(new InvalidOperationException(SecretMessage), "Operation failed");
        await host.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsOnlyTheExceptionType_ThroughTheRealOtlpExporter()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var url = $"http://localhost:{GetFreePort()}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();

        var bodies = new List<byte[]>();
        var serve = ServeAsync(listener, bodies, timeout.Token);

        await LogErrorThroughRealWiringAsync(url, timeout.Token);

        listener.Stop();
        await serve.WaitAsync(timeout.Token);

        string exported;
        lock (bodies)
        {
            Assert.NotEmpty(bodies);
            exported = string.Concat(bodies.Select(Encoding.Latin1.GetString));
        }

        Assert.Contains(ExceptionRedactionLogProcessor.ExceptionTypeKey, exported, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), exported, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-SECRET", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.message", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("exception.stacktrace", exported, StringComparison.Ordinal);
    }
}
