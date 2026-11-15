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
    private static readonly ConcurrentDictionary<Type, Entry> _entries = new();
    private static long _version;

    /// <summary>Stores the parameter for the next visit of the page of <paramref name="viewModelType"/>.</summary>
    /// <param name="viewModelType">The view model that will receive the parameter.</param>
    /// <param name="parameter">The value to hand over; may be <see langword="null"/>.</param>
    /// <returns>
    /// A version for this value. <see cref="Discard(Type, long)"/> with it removes the value only while it is still
    /// the stored one, so a later <see cref="Set"/> for the same page is not dropped by an earlier cleanup.
    /// </returns>
    internal static long Set(Type viewModelType, object? parameter)
    {
        var version = Interlocked.Increment(ref _version);
        _entries[viewModelType] = new Entry(version, parameter);

        return version;
    }

    /// <summary>Returns and removes the parameter stored for <paramref name="viewModelType"/>, or <see langword="null"/>.</summary>
    /// <param name="viewModelType">The view model that receives the parameter.</param>
    internal static object? Take(Type viewModelType) => _entries.TryRemove(viewModelType, out var entry) ? entry.Value : null;

    /// <summary>Removes the parameter stored for <paramref name="viewModelType"/> without returning it; does nothing when none is stored.</summary>
    /// <param name="viewModelType">The view model whose unclaimed parameter is dropped.</param>
    internal static void Discard(Type viewModelType) => _ = _entries.TryRemove(viewModelType, out _);

    /// <summary>
    /// Removes the parameter stored for <paramref name="viewModelType"/> only when it is still the one
    /// <see cref="Set"/> returned <paramref name="version"/> for; a newer value stays.
    /// </summary>
    /// <param name="viewModelType">The view model whose unclaimed parameter is dropped.</param>
    /// <param name="version">The version <see cref="Set"/> returned.</param>
    internal static void Discard(Type viewModelType, long version)
    {
        // Remove by key and entry together, so a newer Set between the read and the remove is left alone.
        while (_entries.TryGetValue(viewModelType, out var current) && current.Version == version)
        {
            if (_entries.TryRemove(new KeyValuePair<Type, Entry>(viewModelType, current)))
            {
                return;
            }
        }
    }

    private readonly record struct Entry(long Version, object? Value);
}
