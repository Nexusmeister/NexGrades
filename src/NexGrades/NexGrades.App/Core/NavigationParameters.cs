using System.Collections.Concurrent;

namespace NexGrades.App.Core;

/// <summary>
/// One-shot hand-over of a navigation parameter to the view model of the page being opened, keyed by view
/// model type. It is static because the WPF-UI page provider constructs the page and its view model inside
/// <c>Navigate</c>, so the caller has no instance to give the value to beforehand. Each value is taken at
/// most once, so a stale parameter never leaks into a later visit.
/// </summary>
internal static class NavigationParameters
{
    private static readonly ConcurrentDictionary<Type, object?> _entries = new();

    /// <summary>Stores the parameter for the next visit of the page of <paramref name="viewModelType"/>.</summary>
    /// <param name="viewModelType">The view model that will receive the parameter.</param>
    /// <param name="parameter">The value to hand over; may be <see langword="null"/>.</param>
    internal static void Set(Type viewModelType, object? parameter) => _entries[viewModelType] = parameter;

    /// <summary>Returns and removes the parameter stored for <paramref name="viewModelType"/>, or <see langword="null"/>.</summary>
    /// <param name="viewModelType">The view model that receives the parameter.</param>
    internal static object? Take(Type viewModelType) => _entries.TryRemove(viewModelType, out var parameter) ? parameter : null;
}
