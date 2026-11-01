using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Infrastructure;
using NexGrades.Common.Storage;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Services;
using NexGrades.ServiceDefaults;

namespace NexGrades.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = CreateHost();

    private static IHost CreateHost()
    {
        var host = Host.CreateApplicationBuilder();

        // OpenTelemetry logs, metrics and traces. Exported only when OTEL_EXPORTER_OTLP_ENDPOINT is set (Aspire).
        _ = host.AddServiceDefaults(NexGradesTelemetry.Name);

        host.Services.AddGradeServices()
            .AddFrontendServices();
        host.AddDatabase();

        return host.Build();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    [SuppressMessage("Usage", "VSTHRD100:Avoid async void methods", Justification = "WPF event handler; all failures are caught below.")]
    private async void OnStartup(object sender, StartupEventArgs e)
    {
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
            // Failures are not logged yet (tracked in #51). Until then at least do not fail silently with no window.
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
            // Not logged: there is no logging yet (#51), and the exception message could carry configuration values.
            Debug.WriteLine($"Telemetry is disabled: {ex.GetType().Name}");
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
    private async void OnExit(object sender, ExitEventArgs e)
    {
        await _host.StopAsync();

        _host.Dispose();
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}
