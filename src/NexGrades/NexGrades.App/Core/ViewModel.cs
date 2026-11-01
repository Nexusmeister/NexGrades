using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Core;

/// <summary>
/// Base class for all view models. It forwards the WPF-UI navigation callbacks to the synchronous hooks
/// <c>OnNavigatedTo</c> and <c>OnNavigatedFrom</c>, which subclasses override when they need no async work.
/// </summary>
public abstract partial class ViewModel : ObservableObject, INavigationAware
{
    /// <inheritdoc />
    public virtual Task OnNavigatedToAsync()
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
