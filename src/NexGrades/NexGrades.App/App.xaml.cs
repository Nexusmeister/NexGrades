using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.Data.Services;
using System.Windows;
using System.Windows.Threading;
using NexGrades.App.Features.Home;
using NexGrades.App.Shell;
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
        host.Services.AddGradeServices()
            .AddFrontendServices()
            .AddDatabase(host.Configuration);

        return host.Build();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            await using var scope = _host.Services.CreateAsyncScope();
            var mig = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
            await mig.RunAsync(CancellationToken.None);

            await _host.StartAsync();
        }
        catch (Exception e1)
        {
            // TODO Some logging
        }
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
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

