using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NexGrades.App.Updates;

/// <summary>
/// Runs <see cref="UpdateCoordinator.CheckOnStartupAsync"/> once, about ten seconds after the host started (the window
/// is shown by then), so the check never competes with the first screen. Nothing it does can fail the app, and it
/// never delays the app's exit.
/// </summary>
public sealed partial class UpdateStartupService : BackgroundService
{
    /// <summary>The wait between the start of the host and the update check.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(10);

    private readonly UpdateCoordinator _coordinator;
    private readonly ILogger<UpdateStartupService> _logger;
    private readonly TimeSpan _delay;

    /// <summary>Creates the service with the <see cref="DefaultDelay"/>.</summary>
    /// <param name="coordinator">Runs the check.</param>
    /// <param name="logger">Logs a failed check.</param>
    public UpdateStartupService(UpdateCoordinator coordinator, ILogger<UpdateStartupService> logger)
        : this(coordinator, logger, DefaultDelay)
    {
    }

    /// <summary>Creates the service with its own delay, for tests.</summary>
    /// <param name="coordinator">Runs the check.</param>
    /// <param name="logger">Logs a failed check.</param>
    /// <param name="delay">The wait before the check.</param>
    internal UpdateStartupService(UpdateCoordinator coordinator, ILogger<UpdateStartupService> logger, TimeSpan delay)
    {
        _coordinator = coordinator;
        _logger = logger;
        _delay = delay;
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The update check is optional; no failure of it may stop the host or the app.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_delay, stoppingToken);
            await _coordinator.CheckOnStartupAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The app is closing.
        }
        catch (Exception ex)
        {
            LogStartupCheckFailed(ex);
        }
    }

    /// <summary>
    /// Cancels a check that is still waiting or running, without waiting for it. The app's exit handler awaits the host's
    /// stop on the UI thread, which is already shutting down: a stop that does not complete at once means its
    /// continuation never runs, so the host is not disposed and the last log lines are not written.
    /// </summary>
    /// <param name="cancellationToken">Not used; the stop completes at once.</param>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // The check catches every failure, so the discarded task never faults.
        _ = base.StopAsync(cancellationToken);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The update check after startup failed")]
    private partial void LogStartupCheckFailed(Exception exception);
}
