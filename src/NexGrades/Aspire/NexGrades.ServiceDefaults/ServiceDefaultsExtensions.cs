using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace NexGrades.ServiceDefaults;

/// <summary>
/// The Aspire service defaults, trimmed to what a desktop application needs: OpenTelemetry logs, metrics and
/// traces. See docs/decisions/0002-telemetry-and-privacy.md for the reasoning and the privacy rules.
/// </summary>
/// <remarks>
/// Left out of the stock template on purpose: ASP.NET Core instrumentation and <c>MapDefaultEndpoints</c> (there is
/// no web server), health checks (nothing probes the app, there is no endpoint), service discovery (the app calls
/// fixed public URLs, and the Aspire resources it uses reach it as connection strings) and the HttpClient
/// resilience handler (nothing uses an HttpClient yet, and its 10 second attempt timeout would break large
/// downloads such as the updater, #21).
/// </remarks>
public static class ServiceDefaultsExtensions
{
    /// <summary>Name of the environment variable / configuration key that switches the OTLP exporter on.</summary>
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Name of EF Core's built-in meter (queries, SaveChanges, DbContext counts, ...).</summary>
    private const string EntityFrameworkCoreMeterName = "Microsoft.EntityFrameworkCore";

    /// <summary>
    /// Adds OpenTelemetry logs, metrics and traces. Telemetry leaves the process only when
    /// <see cref="OtlpEndpointKey"/> is configured (which the Aspire AppHost does); otherwise nothing is exported.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="appTelemetryName">
    /// Name of the app's own activity source and meter (<c>NexGradesTelemetry.Name</c>). Passed in so this
    /// project stays free of any reference to the app's assemblies.
    /// </param>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder, string appTelemetryName)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(appTelemetryName);

        return builder.ConfigureOpenTelemetry(appTelemetryName);
    }

    /// <summary>
    /// Configures OpenTelemetry logging, metrics and tracing, and the OTLP exporter when an endpoint is set.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="appTelemetryName">Name of the app's own activity source and meter.</param>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder, string appTelemetryName)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(appTelemetryName);

        _ = builder.Logging.AddOpenTelemetry(logging =>
        {
            // Privacy: log messages are never rendered into the export, and scopes are not exported. Structured
            // log arguments still travel as attributes, so code must not log pupil data at all.
            logging.IncludeFormattedMessage = false;
            logging.IncludeScopes = false;

            // Exception messages and stack traces are not exported either, only the exception type. This is a
            // processor of the same pipeline as the exporter and is registered before UseOtlpExporter below.
            _ = logging.AddProcessor(new ExceptionRedactionLogProcessor());
        });

        // EF Core logs executed SQL text at Information level. Keep that out of the exported logs (and the dashboard).
        _ = builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

        _ = builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(EntityFrameworkCoreMeterName)
                .AddMeter(appTelemetryName))
            .WithTracing(tracing => tracing
                .AddSource(appTelemetryName)
                .AddHttpClientInstrumentation());

        // The only switch for export. No endpoint, no exporter, no network traffic for telemetry.
        if (IsOtlpExportEnabled(builder.Configuration))
        {
            _ = builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Whether telemetry is exported: true only when <see cref="OtlpEndpointKey"/> holds a non-blank value.
    /// </summary>
    public static bool IsOtlpExportEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return !string.IsNullOrWhiteSpace(configuration[OtlpEndpointKey]);
    }

    /// <summary>
    /// Creates the tracer and meter providers now. The OpenTelemetry hosted service would do so in
    /// <c>IHost.StartAsync</c>, but the app does work (database migration, the startup span) before that, and
    /// listeners that do not exist yet would miss it. Call this before anything that emits telemetry.
    /// </summary>
    public static IServiceProvider InitializeTelemetry(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.GetService<TracerProvider>();
        _ = services.GetService<MeterProvider>();

        return services;
    }
}
