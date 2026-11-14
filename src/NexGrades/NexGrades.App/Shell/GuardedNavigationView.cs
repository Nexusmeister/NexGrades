using System.Windows;
using NexGrades.App.Core;
using Wpf.Ui.Controls;

namespace NexGrades.App.Shell;

/// <summary>
/// A <see cref="NavigationView"/> whose back button asks <see cref="PageNavigator"/> whether the current page
/// may be left. It is styled by the implicit style in <c>ShellControlStyles.xaml</c>. The WPF-UI back button calls <c>GoBack</c> directly, without raising the <c>Navigating</c>
/// event, so the shell's navigation guard would not see it otherwise.
/// </summary>
public sealed class GuardedNavigationView : NavigationView
{
    /// <summary>The navigator that decides whether the back button may proceed; unset means no guard.</summary>
    public PageNavigator? Guard { get; set; }

    /// <inheritdoc />
    protected override void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        if (Guard?.TryGuardBack(GoBack) == true)
        {
            return;
        }

        base.OnBackButtonClick(sender, e);
    }
}
