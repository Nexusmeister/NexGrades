using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Home;
using NexGrades.App.Infrastructure;
using NexGrades.App.Shell;
using NexGrades.Common.Storage;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Services;
using Wpf.Ui;

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
        // Creates the trace and meter listeners now rather than in _host.StartAsync, so the startup span and the
        // migration are not missed.
        _ = _host.Services.InitializeTelemetry();

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
            RecordStartupDuration(NexGradesTelemetry.Outcomes.Success);
        }
        catch (Exception ex)
        {
            NexGradesTelemetry.MarkFailed(startup, ex);
            RecordStartupDuration(NexGradesTelemetry.Outcomes.Error);
            // TODO Some logging (#51). Until then at least do not fail silently with no window.
            MessageBox.Show("NexGrades could not start. Check that its data folder is writable.", "NexGrades", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void RecordStartupDuration(string outcome)
    {
        using var process = Process.GetCurrentProcess();
        NexGradesTelemetry.StartupDuration.Record(
            (DateTime.Now - process.StartTime).TotalSeconds,
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

public class ApplicationHostService(IServiceProvider serviceProvider) : IHostedService
{
    private INavigationWindow? _navigationWindow;

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await HandleActivationAsync();
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates main window during activation.
    /// </summary>
    private async Task HandleActivationAsync()
    {
        await Task.CompletedTask;

        if (!Application.Current.Windows.OfType<MainWindow>().Any())
        {
            _navigationWindow = (serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow)!;
            _navigationWindow!.ShowWindow();

            _ = _navigationWindow.Navigate(typeof(HomePage));
        }

        await Task.CompletedTask;
    }
}

