using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.App.Extensions;
using NexGrades.App.Infrastructure;
using NexGrades.App.Logging;
using NexGrades.Common.Storage;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Services;
using NexGrades.ServiceDefaults;
using Serilog.Extensions.Logging;

namespace NexGrades.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // Held in static fields so the crash handlers outlive CreateHost and cannot be collected.
    private static UnhandledExceptionLogger? _crashLogger;
    private static SerilogLoggerFactory? _crashLoggerFactory;

    private static readonly IHost _host = CreateHost();

    private static ILogger Logger => _host.Services.GetRequiredService<ILogger<App>>();

    private static IHost CreateHost()
    {
        var host = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Settings files sit next to the executable, whatever the working directory is.
            ContentRootPath = AppContext.BaseDirectory,
        });

        // Logging first: it clears the default providers, which would also remove the OpenTelemetry one added below.
        _ = host.AddAppLogging();
        InstallCrashHandlers();

        try
        {
            // OpenTelemetry logs, metrics and traces. Exported only when OTEL_EXPORTER_OTLP_ENDPOINT is set (Aspire).
            _ = host.AddServiceDefaults(NexGradesTelemetry.Name);

            host.Services.AddGradeServices()
                .AddFrontendServices();
            host.AddDatabase();

            return host.Build();
        }
        catch (Exception ex)
        {
            // The host is not built, so the file logger is reached through the crash logger's factory.
            var logger = _crashLoggerFactory!.CreateLogger<App>();
            LogHostCreationFailed(logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Records unhandled exceptions from any thread and unobserved task exceptions in the log file, flushing
    /// before a terminating crash. Installed as soon as the file logger exists.
    /// </summary>
    private static void InstallCrashHandlers()
    {
        _crashLoggerFactory = new SerilogLoggerFactory(Serilog.Log.Logger);
        var crashLogger = new UnhandledExceptionLogger(_crashLoggerFactory.CreateLogger<UnhandledExceptionLogger>(), FileLogging.Flush);
        _crashLogger = crashLogger;

        AppDomain.CurrentDomain.UnhandledException += (_, e) => crashLogger.OnDomainException(e.ExceptionObject, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            crashLogger.OnUnobservedTaskException(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handler; all failures are caught below.")]
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        StartupDiagnostics.LogStartup(
            Logger,
            _host.Services.GetRequiredService<IHostEnvironment>(),
            _host.Services.GetRequiredService<DatabaseLocation>(),
            _host.Services.GetRequiredService<IAppDataPaths>());

        InitializeTelemetrySafely();

        using var startup = NexGradesTelemetry.ActivitySource.StartActivity("App.Startup");
        try
        {
            var location = _host.Services.GetRequiredService<DatabaseLocation>();
            if (!location.IsOverride)
            {
                // Must happen before any connection opens the database file.
                var paths = _host.Services.GetRequiredService<IAppDataPaths>();
                paths.EnsureCreated();
                _host.Services.GetRequiredService<LegacyDatabaseRelocator>()
                    .Relocate([AppContext.BaseDirectory, Environment.CurrentDirectory], paths.DatabaseFilePath);
            }

            await using var scope = _host.Services.CreateAsyncScope();
            var mig = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
            await mig.RunAsync(CancellationToken.None);

            await _host.StartAsync();

            // The window is shown by ApplicationHostService.StartAsync, so this is the time to a usable UI.
            _ = startup?.SetTag(NexGradesTelemetry.OutcomeTag, NexGradesTelemetry.Outcomes.Success);
            RecordStartupDuration(NexGradesTelemetry.Outcomes.Success);
        }
        catch (Exception ex)
        {
            NexGradesTelemetry.MarkFailed(startup, ex);
            _ = startup?.SetTag(NexGradesTelemetry.OutcomeTag, NexGradesTelemetry.Outcomes.Error);
            RecordStartupDuration(NexGradesTelemetry.Outcomes.Error);
            LogStartupFailed(Logger, ex);
            // Do not fail silently with no window.
            MessageBox.Show("NexGrades could not start. Check that its data folder is writable.", "NexGrades", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Creates the trace and meter listeners now rather than in <c>_host.StartAsync</c>, so the startup span and the
    /// migration are not missed. Telemetry is a diagnostic aid: if it cannot be set up (for example the exporter
    /// rejects its configuration), the app runs without it instead of failing to start. Only this call is guarded.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Telemetry must never prevent the app from starting.")]
    private static void InitializeTelemetrySafely()
    {
        try
        {
            _ = _host.Services.InitializeTelemetry();
        }
        catch (Exception ex)
        {
            // Only the type is logged: the exception message could carry configuration values.
            var exceptionType = ex.GetType().Name;
            LogTelemetryDisabled(Logger, exceptionType);
        }
    }

    private static void RecordStartupDuration(string outcome)
    {
        using var process = Process.GetCurrentProcess();
        NexGradesTelemetry.StartupDuration.Record(
            // Both sides in UTC so a daylight saving change since process start cannot skew the duration.
            (DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds,
            new KeyValuePair<string, object?>(NexGradesTelemetry.OutcomeTag, outcome));
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handler.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed host shutdown is logged; the process is exiting and there is nothing else to do with it.")]
    private async void OnExit(object sender, ExitEventArgs e)
    {
        var logger = Logger;
        StartupDiagnostics.LogShuttingDown(logger);
        try
        {
            await _host.StopAsync();
        }
        catch (Exception ex)
        {
            LogStopFailed(logger, ex);
        }

        StartupDiagnostics.LogShutdown(logger);
        _host.Dispose();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled. It is only logged; <c>Handled</c>
    /// stays false, so the app's behaviour on a crash is unchanged.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
        _crashLogger?.OnDispatcherException(e.Exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The application host could not be created")]
    private static partial void LogHostCreationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "NexGrades could not start")]
    private static partial void LogStartupFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telemetry is disabled: {ExceptionType}")]
    private static partial void LogTelemetryDisabled(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "The application host did not stop cleanly")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
