using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Core;

/// <summary>
/// Base class for all view models. It forwards the WPF-UI navigation callbacks to the synchronous hooks
/// <c>OnNavigatedTo</c> and <c>OnNavigatedFrom</c>, which subclasses override when they need no async work.
/// A view model that needs a value from the page that opened it (for example the id to edit) overrides
/// <see cref="OnNavigatedToAsync(object?)"/> and receives what <see cref="PageNavigator"/> passed.
/// </summary>
/// <remarks>
/// A <c>CanLeaveAsync</c> hook that lets a view model veto leaving its page is reserved here; it does not
/// exist yet.
/// </remarks>
public abstract partial class ViewModel : ObservableObject, INavigationAware
{
    /// <inheritdoc />
    public async Task OnNavigatedToAsync()
    {
        var parameter = NavigationParameters.Take(GetType());
        await OnNavigatedToAsync(parameter);
    }

    /// <summary>
    /// Handles the event that is fired after the component is navigated to, with the parameter handed over by
    /// <see cref="PageNavigator"/>, or <see langword="null"/> when there is none. The default calls the
    /// synchronous <see cref="OnNavigatedTo()"/>.
    /// </summary>
    /// <param name="parameter">The navigation parameter, consumed once.</param>
    protected virtual Task OnNavigatedToAsync(object? parameter)
    {
        OnNavigatedTo();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the event that is fired after the component is navigated to.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigatedTo() { }

    /// <inheritdoc />
    public virtual Task OnNavigatedFromAsync()
    {
        OnNavigatedFrom();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the event that is fired before the component is navigated from.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigatedFrom() { }
}
