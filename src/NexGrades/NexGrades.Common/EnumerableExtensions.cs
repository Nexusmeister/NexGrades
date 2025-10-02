using System.Collections.ObjectModel;

namespace NexGrades.Common;

public static class EnumerableExtensions
{
    public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> us) => new(us);
}