using System.Windows;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Features.Home;
using NexGrades.App.Shell;
using Wpf.Ui;

namespace NexGrades.App;

/// <summary>
/// Hosted service that shows the main window and opens the home page when the host starts, unless a main window
/// is already open. Registered by <c>AddGradeServices</c>.
/// </summary>
/// <param name="serviceProvider">Resolves the navigation window and its pages from DI.</param>
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

