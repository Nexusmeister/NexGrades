using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.Common.Storage;
using Serilog;
using Serilog.Extensions.Logging;

namespace NexGrades.App.Logging;

/// <summary>Wires the rolling file log into the host.</summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Replaces the default logging providers with the rolling log file (and the debugger output in Development).
    /// The default Console and EventLog providers are removed on purpose: they would write exception messages,
    /// including those of Domain exceptions that are redacted in the file. Levels come from the <c>Logging</c>
    /// section of the app settings.
    /// </summary>
    /// <remarks>
    /// Call this before <c>AddServiceDefaults</c>: it clears all providers, which would also remove the
    /// OpenTelemetry one. If the log folder is unusable the app runs without a file log.
    /// </remarks>
    /// <param name="builder">The host builder to configure.</param>
    /// <param name="paths">Where the log folder is; defaults to the per-user data folder.</param>
    public static HostApplicationBuilder AddAppLogging(this HostApplicationBuilder builder, IAppDataPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Logging.ClearProviders();

        var options = FileLoggingOptions.FromConfiguration(builder.Configuration);
        var logger = FileLogging.TryCreate(paths ?? AppDataPaths.CreateDefault(), options);
        if (logger is not null)
        {
            // Crash handlers flush through Log.CloseAndFlush, so the same instance must be the global one.
            Log.Logger = logger;
            _ = builder.Logging.AddSerilog(logger, dispose: true);

            // AddSerilog registers a provider-specific Trace rule that outranks the Logging:LogLevel section, and
            // the Serilog logger itself has no minimum level, so without this everything would reach the file.
            // Dropping the rule lets the configured levels decide.
            _ = builder.Services.Configure<LoggerFilterOptions>(filterOptions =>
            {
                var serilogProvider = typeof(SerilogLoggerProvider).FullName;
                var injected = filterOptions.Rules
                    .Where(rule => rule.ProviderName == serilogProvider && rule.CategoryName is null && rule.LogLevel == LogLevel.Trace)
                    .ToList();
                foreach (var rule in injected)
                {
                    _ = filterOptions.Rules.Remove(rule);
                }
            });
        }

        if (builder.Environment.IsDevelopment())
        {
            _ = builder.Logging.AddDebug();
        }

        return builder;
    }
}
