using System.Collections.ObjectModel;

namespace NexGrades.Common;

/// <summary>
/// Helpers that turn query results into collections that WPF can bind to.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Copies <paramref name="us"/> into a new <see cref="ObservableCollection{T}"/>. The copy is taken
    /// immediately, so later changes to the source sequence do not reach the returned collection.
    /// </summary>
    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> us) => new(us);
}
