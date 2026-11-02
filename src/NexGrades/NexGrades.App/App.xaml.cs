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
using NexGrades.Domain.Services;
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

    // Created in OnStartup, inside its try, so a failure while building it is reported like any other startup failure.
    private static IHost? _host;

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

        // OpenTelemetry logs, metrics and traces. Exported only when OTEL_EXPORTER_OTLP_ENDPOINT is set (Aspire).
        _ = host.AddServiceDefaults(NexGradesTelemetry.Name);

        host.Services.AddGradeServices()
            .AddFrontendServices();
        host.AddDatabase();

        return host.Build();
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
    /// Occurs when the application is loading. One try covers every startup step, including creating the host, so
    /// a failure at any point is logged, shown to the user once and ends the process with a non-zero exit code.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handler; all failures are caught below.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any startup failure, whatever its type, must end in the error dialog and a non-zero exit instead of an unhandled exception.")]
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        Activity? startup = null;
        try
        {
            var host = _host = CreateHost();

            StartupDiagnostics.LogStartup(
                host.Services.GetRequiredService<ILogger<App>>(),
                host.Services.GetRequiredService<ILoggerFactory>(),
                host.Services.GetRequiredService<IHostEnvironment>(),
                host.Services.GetRequiredService<DatabaseLocation>(),
                host.Services.GetRequiredService<IAppDataPaths>());

            InitializeTelemetrySafely(host);

            startup = NexGradesTelemetry.ActivitySource.StartActivity("App.Startup");

            var location = host.Services.GetRequiredService<DatabaseLocation>();
            if (!location.IsOverride)
            {
                // Must happen before any connection opens the database file.
                var paths = host.Services.GetRequiredService<IAppDataPaths>();
                paths.EnsureCreated();
                host.Services.GetRequiredService<LegacyDatabaseRelocator>()
                    .Relocate([AppContext.BaseDirectory, Environment.CurrentDirectory], paths.DatabaseFilePath);
            }

            await using var scope = host.Services.CreateAsyncScope();
            var mig = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
            await mig.RunAsync(CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<SchoolYearService>().EnsureActiveYearAsync(CancellationToken.None);

            await host.StartAsync();

            // The window is shown by ApplicationHostService.StartAsync, so this is the time to a usable UI.
            _ = startup?.SetTag(NexGradesTelemetry.OutcomeTag, NexGradesTelemetry.Outcomes.Success);
            RecordStartupDuration(NexGradesTelemetry.Outcomes.Success);
        }
        catch (Exception ex)
        {
            ReportStartupFailure(ex, startup);
        }
        finally
        {
            startup?.Dispose();
        }
    }

    /// <summary>
    /// Hands a startup failure to <see cref="StartupFailureHandler"/>. Everything that can throw (telemetry, the log
    /// folder) is passed as a delegate so the handler runs it guarded and always reaches the dialog and the exit.
    /// </summary>
    private void ReportStartupFailure(Exception exception, Activity? startup)
    {
        var host = _host;
        new StartupFailureHandler(new MessageBoxStartupErrorDialog(), ExitWithCode).Handle(
            exception,
            () => host?.Services.GetService<ILogger<App>>(),
            () => _crashLoggerFactory?.CreateLogger<App>(),
            // Only mention the folder when the file logger was actually created.
            () => FileLogging.IsActive ? AppDataPaths.CreateDefault().LogDirectory : null,
            failure =>
            {
                if (host is null)
                {
                    return;
                }

                NexGradesTelemetry.MarkFailed(startup, failure);
                _ = startup?.SetTag(NexGradesTelemetry.OutcomeTag, NexGradesTelemetry.Outcomes.Error);
                RecordStartupDuration(NexGradesTelemetry.Outcomes.Error);
            });
    }

    /// <summary>
    /// Ends the application with <paramref name="exitCode"/>. <see cref="Environment.ExitCode"/> is set as well,
    /// because the generated <c>Main</c> discards the value <c>Run</c> returns.
    /// </summary>
    private void ExitWithCode(int exitCode)
    {
        Environment.ExitCode = exitCode;
        Shutdown(exitCode);
    }

    /// <summary>
    /// Creates the trace and meter listeners now rather than in <c>_host.StartAsync</c>, so the startup span and the
    /// migration are not missed. Telemetry is a diagnostic aid: if it cannot be set up (for example the exporter
    /// rejects its configuration), the app runs without it instead of failing to start. Only this call is guarded.
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Telemetry must never prevent the app from starting.")]
    private static void InitializeTelemetrySafely(IHost host)
    {
        try
        {
            _ = host.Services.InitializeTelemetry();
        }
        catch (Exception ex)
        {
            // Only the type is logged: the exception message could carry configuration values.
            var exceptionType = ex.GetType().Name;
            LogTelemetryDisabled(host.Services.GetRequiredService<ILogger<App>>(), exceptionType);
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
        var host = _host;
        if (host is null)
        {
            // The start failed before the host existed; only the file log, if any, needs closing.
            FileLogging.Flush();
            return;
        }

        var logger = host.Services.GetRequiredService<ILogger<App>>();
        StartupDiagnostics.LogShuttingDown(logger);
        try
        {
            await host.StopAsync();
        }
        catch (Exception ex)
        {
            LogStopFailed(logger, ex);
        }

        StartupDiagnostics.LogShutdown(logger);
        host.Dispose();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled. It is only logged; <c>Handled</c>
    /// stays false, so the app's behaviour on a crash is unchanged.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) =>
        _crashLogger?.OnDispatcherException(e.Exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Telemetry is disabled: {ExceptionType}")]
    private static partial void LogTelemetryDisabled(ILogger logger, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "The application host did not stop cleanly")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
