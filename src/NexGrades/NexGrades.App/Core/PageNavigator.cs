using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Core;

/// <summary>
/// Opens a page below the current one and hands a parameter to its view model, which receives it in
/// <see cref="ViewModel.OnNavigatedToAsync(object?)"/>. It replaces per-page "edit request" singletons.
/// It also guards leaving the current page: the shell asks it on every navigation and on the back button, and
/// it consults <see cref="ViewModel.CanLeaveAsync(CancellationToken)"/> of the page being left.
/// </summary>
public sealed partial class PageNavigator
{
    private readonly INavigationService _navigation;
    private readonly ILogger<PageNavigator> _logger;
    private readonly Action<Action> _scheduleCleanup;

    private ViewModel? _current;
    private bool _guardActive;
    private bool _bypass;
    private Func<bool>? _request;

    /// <summary>Creates the navigator.</summary>
    /// <param name="navigation">The WPF-UI navigation service that performs the navigation.</param>
    /// <param name="logger">Logs guard failures; never with page content.</param>
    public PageNavigator(INavigationService navigation, ILogger<PageNavigator> logger)
        : this(navigation, logger, ScheduleOnUiIdle)
    {
    }

    /// <summary>Creates the navigator with its own scheduler for the deferred parameter cleanup, for tests.</summary>
    /// <param name="navigation">The WPF-UI navigation service that performs the navigation.</param>
    /// <param name="logger">Logs guard failures.</param>
    /// <param name="scheduleCleanup">Runs the given action once the navigation callbacks have finished.</param>
    internal PageNavigator(INavigationService navigation, ILogger<PageNavigator> logger, Action<Action> scheduleCleanup)
    {
        _navigation = navigation;
        _logger = logger;
        _scheduleCleanup = scheduleCleanup;
    }

    /// <summary>
    /// Navigates to <typeparamref name="TPage"/> keeping the back stack, passing <paramref name="parameter"/> to
    /// the page's view model. The pages set their own <c>DataContext</c>, so the data-context overload of
    /// <c>Navigate</c> cannot carry the value.
    /// </summary>
    /// <typeparam name="TPage">A page that implements <see cref="INavigableView{T}"/>.</typeparam>
    /// <param name="parameter">The value for the view model, or <see langword="null"/> for none.</param>
    /// <returns>
    /// <see langword="true"/> when the navigation happened. It is <see langword="false"/> when the page being left
    /// vetoed it; if the user then confirms, the navigation is issued again by itself.
    /// </returns>
    public bool NavigateWithHierarchy<TPage>(object? parameter = null)
    {
        var viewModelType = typeof(TPage).GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INavigableView<>))
            .GetGenericArguments()[0];

        NavigationParameters.Set(viewModelType, parameter);

        // Lets the guard re-issue exactly this call, parameter included, once the user confirms leaving.
        _request = () => NavigateWithHierarchy<TPage>(parameter);
        try
        {
            var navigated = _navigation.NavigateWithHierarchy(typeof(TPage));
            if (navigated)
            {
                // The view model normally takes the value itself; this drops it if it never got to.
                _scheduleCleanup(() => NavigationParameters.Discard(viewModelType));
            }
            else
            {
                _ = NavigationParameters.Take(viewModelType);
            }

            return navigated;
        }
        catch
        {
            _ = NavigationParameters.Take(viewModelType);
            throw;
        }
        finally
        {
            _request = null;
        }
    }

    /// <summary>Records the view model of the page that is now shown, or <see langword="null"/> if it has none.</summary>
    /// <param name="viewModel">The view model of the current page.</param>
    internal void SetCurrent(ViewModel? viewModel) => _current = viewModel;

    /// <summary>
    /// Called by the shell while a navigation is starting. Returns whether the shell must cancel it. When the
    /// current page needs to ask the user first, it cancels, and re-issues the navigation if the user agrees.
    /// </summary>
    /// <param name="fallbackReissue">
    /// Re-issues the navigation when it did not come from <see cref="NavigateWithHierarchy{TPage}(object?)"/>
    /// (for example a sidebar click); returns whether it navigated.
    /// </param>
    /// <returns><see langword="true"/> when the navigation must be cancelled.</returns>
    internal bool ShouldCancelNavigating(Func<bool> fallbackReissue) => Guard(_request ?? fallbackReissue);

    /// <summary>
    /// Called by the shell when the back button is clicked. Returns whether the shell must skip its own back
    /// navigation because the guard handles it.
    /// </summary>
    /// <param name="goBack">Navigates back; called once the user agrees to leave.</param>
    /// <returns><see langword="true"/> when the normal back navigation must not run.</returns>
    internal bool TryGuardBack(Func<bool> goBack) => Guard(goBack);

    private bool Guard(Func<bool> reissue)
    {
        if (_bypass || _current is not { } current)
        {
            return false;
        }

        // A second click while the prompt is open is swallowed.
        if (_guardActive)
        {
            return true;
        }

        var pending = current.CanLeaveAsync();
        if (pending.IsCompletedSuccessfully)
        {
            return !pending.Result;
        }

        _guardActive = true;
        _ = RunGuardAsync(pending, current, reissue);

        return true;
    }

    [SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "The task comes from the view model's own CanLeaveAsync and is awaited on the UI thread without blocking.")]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever the page's check throws, the safe outcome is to stay on the page.")]
    private async Task RunGuardAsync(Task<bool> pending, ViewModel guarded, Func<bool> reissue)
    {
        var mayLeave = false;
        try
        {
            mayLeave = await pending;
        }
        catch (Exception ex)
        {
            // Staying on the page is the safe outcome.
            LogGuardFailed(ex);
        }
        finally
        {
            _guardActive = false;
        }

        if (!mayLeave || !ReferenceEquals(_current, guarded))
        {
            return;
        }

        _bypass = true;
        try
        {
            _ = reissue();
        }
        finally
        {
            _bypass = false;
        }
    }

    [SuppressMessage("Usage", "VSTHRD001:Avoid legacy thread switching APIs", Justification = "Only defers a cleanup until the navigation callbacks finished; no thread switch is awaited.")]
    private static void ScheduleOnUiIdle(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, action);
        }
        else
        {
            action();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Checking whether the page may be left failed; staying on the page.")]
    private partial void LogGuardFailed(Exception exception);
}
