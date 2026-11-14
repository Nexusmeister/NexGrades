using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NexGrades.App.Shell;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards against the startup crash where <see cref="GuardedNavigationView"/> got no WPF-UI template, so the first
/// navigation threw a NullReferenceException inside <c>NavigationView.UpdateContent</c>.
/// </summary>
public sealed class GuardedNavigationViewTests
{
    [Fact]
    public void ApplyTemplate_CreatesTemplateAndContentPresenter_ForGuardedNavigationView()
    {
        RunOnStaThread(() =>
        {
            var window = CreateHostWindow(out var navigation);
            try
            {
                window.Show();
                LayOut(window);

                Assert.NotNull(navigation.Template);
                Assert.NotNull(FindDescendant<NavigationViewContentPresenter>(navigation));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Navigate_DoesNotThrow_ForGuardedNavigationViewWithThemeStyles()
    {
        RunOnStaThread(() =>
        {
            var window = CreateHostWindow(out var navigation);
            try
            {
                window.Show();
                LayOut(window);

                var exception = Record.Exception(() => navigation.Navigate(typeof(TrivialPage)));

                Assert.Null(exception);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Window CreateHostWindow(out GuardedNavigationView navigation)
    {
        // Mirrors App.xaml: the WPF-UI theme plus the shared shell styles (which include the control dictionary).
        var window = new Window { Width = 800, Height = 600 };
        window.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Dark });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/NexGrades.App;component/Shell/ShellControlStyles.xaml") });

        navigation = new GuardedNavigationView();
        navigation.SetPageProviderService(new ActivatorPageProvider());
        window.Content = navigation;
        return window;
    }

    private static void LayOut(Window window)
    {
        window.Measure(new Size(800, 600));
        window.Arrange(new Rect(0, 0, 800, 600));
        window.ApplyTemplate();
        window.UpdateLayout();
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    private sealed class ActivatorPageProvider : Wpf.Ui.Abstractions.INavigationViewPageProvider
    {
        public object? GetPage(Type pageType) => Activator.CreateInstance(pageType);
    }

    /// <summary>A minimal page to navigate to.</summary>
    public sealed class TrivialPage : Page
    {
    }
}
