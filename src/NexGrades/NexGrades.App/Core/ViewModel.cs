using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Core;

/// <summary>
/// Base class for all view models. It forwards the WPF-UI navigation callbacks to the synchronous hooks
/// <c>OnNavigatedTo</c> and <c>OnNavigatedFrom</c>, which subclasses override when they need no async work.
/// A view model that needs a value from the page that opened it (for example the id to edit) overrides
/// <see cref="OnNavigatedToAsync(object?)"/> and receives what <see cref="PageNavigator"/> passed.
/// </summary>
public abstract partial class ViewModel : ObservableObject, INavigationAware
{
    /// <summary>
    /// Asked before the user leaves the page, by sidebar navigation or the back button. A page that holds
    /// unsaved input overrides it to ask for confirmation. The default always allows leaving.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when the page may be left; <see langword="false"/> to stay.</returns>
    public virtual Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

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
