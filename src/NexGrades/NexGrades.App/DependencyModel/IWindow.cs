using System.Windows;

namespace NexGrades.App.DependencyModel;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}