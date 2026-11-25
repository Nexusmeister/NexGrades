using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.Students;
using NexGrades.App.Tests.TestSupport;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace NexGrades.App.Tests.Composition;

/// <summary>
/// Guards against a content dialog that loses its WPF-UI template. A <see cref="ContentDialog"/> subclass is not
/// found by the implicit style of its base class, so without an explicit <c>Style</c> it renders as bare content with
/// no chrome and no buttons. The dialogs are shown in a dialog host laid out at a small window size.
/// </summary>
[Collection(SharedTelemetryState.Name)]
public class DialogChromeTests
{
    private const int WindowWidth = 900;
    private const int WindowHeight = 600;

    [Fact]
    public void UnlockYearDialog_ShowsPrimaryAndCloseButtons_InsideSmallWindow()
    {
        SharedStaThread.Run(() =>
        {
            var clock = new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local));
            AssertButtonsInsideArea(host => new UnlockYearDialog(host, new UnlockYearDialogViewModel("2025/26", clock)), "Unlock", "Cancel");
        });
    }

    [Fact]
    public void ExportNotesDialog_ShowsPrimaryAndCloseButtons_InsideSmallWindow()
    {
        SharedStaThread.Run(() => AssertButtonsInsideArea(
                host => new ExportNotesDialog(host, new ExportNotesDialogViewModel(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))),
                "Export",
                "Cancel"));
    }

    [Fact]
    public void ExportNotesDialog_ShowsPrimaryAndCloseButtons_ForReversedRange()
    {
        SharedStaThread.Run(() => AssertButtonsInsideArea(
                host => new ExportNotesDialog(host, new ExportNotesDialogViewModel(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1))),
                "Export",
                "Cancel"));
    }

    [Fact]
    public void ExportNotesDialog_StaysOpen_WhenPrimaryIsClickedWithReversedRange()
    {
        SharedStaThread.Run(() =>
        {
            var viewModel = new ExportNotesDialogViewModel(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1));
            var (dialog, closing) = ShowAndClickPrimary(host => new ExportNotesDialog(host, viewModel));

            Assert.Null(closing);
            Assert.True(viewModel.HasValidationMessage);
            var button = FindByName<System.Windows.Controls.Button>(dialog, "PrimaryButton");
            Assert.True(button is { Visibility: Visibility.Visible, ActualWidth: > 0 });
        });
    }

    [Fact]
    public void ExportNotesDialog_ClosesWithPrimaryResult_WhenPrimaryIsClickedWithValidRange()
    {
        SharedStaThread.Run(() =>
        {
            var viewModel = new ExportNotesDialogViewModel(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            var (_, closing) = ShowAndClickPrimary(host => new ExportNotesDialog(host, viewModel));

            Assert.NotNull(closing);
            Assert.Equal(ContentDialogResult.Primary, closing.Result);
        });
    }

    private static (ContentDialog Dialog, ContentDialogClosingEventArgs? Closing) ShowAndClickPrimary(Func<ContentDialogHost, ContentDialog> createDialog)
    {
        EnsureApplicationResources();

        var host = new ContentDialogHost();
        var root = new System.Windows.Controls.Grid { Width = WindowWidth, Height = WindowHeight };
        root.Children.Add(host);

        var dialog = createDialog(host);
        ContentDialogClosingEventArgs? closing = null;
        dialog.Closing += (_, e) => closing = e;
        _ = dialog.ShowAsync();
        root.Measure(new Size(WindowWidth, WindowHeight));
        root.Arrange(new Rect(0, 0, WindowWidth, WindowHeight));
        root.UpdateLayout();

        var button = FindByName<System.Windows.Controls.Button>(dialog, "PrimaryButton");
        Assert.True(button is not null, "PrimaryButton is missing.");
        Assert.True(button.Command?.CanExecute(button.CommandParameter) ?? false, "PrimaryButton has no executable command.");
        button.Command.Execute(button.CommandParameter);

        return (dialog, closing);
    }

    private static void AssertButtonsInsideArea(Func<ContentDialogHost, ContentDialog> createDialog, string primaryText, string closeText)
    {
        EnsureApplicationResources();

        var thread = Thread.CurrentThread;
        var previousUiCulture = thread.CurrentUICulture;
        thread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

        var host = new ContentDialogHost();
        var root = new System.Windows.Controls.Grid { Width = WindowWidth, Height = WindowHeight };
        root.Children.Add(host);

        try
        {
            var dialog = createDialog(host);
            _ = dialog.ShowAsync();

            // Lay out without showing a window, so the result does not depend on the dispatcher state other tests leave.
            root.Measure(new Size(WindowWidth, WindowHeight));
            root.Arrange(new Rect(0, 0, WindowWidth, WindowHeight));
            root.UpdateLayout();

            var bounds = new Rect(0, 0, WindowWidth, WindowHeight);
            AssertButton(dialog, "PrimaryButton", primaryText, root, bounds);
            AssertButton(dialog, "CloseButton", closeText, root, bounds);
        }
        finally
        {
            thread.CurrentUICulture = previousUiCulture;
        }
    }

    private static void AssertButton(ContentDialog dialog, string name, string expectedText, FrameworkElement root, Rect bounds)
    {
        var button = FindByName<System.Windows.Controls.Button>(dialog, name);

        Assert.True(button is not null, $"{name} is missing: the dialog has no WPF-UI template (style set: {dialog.Style is not null}).");
        Assert.Equal(expectedText, button.Content);
        Assert.Equal(Visibility.Visible, button.Visibility);
        Assert.True(button.ActualWidth > 0 && button.ActualHeight > 0, $"{name} has no size.");

        var topLeft = button.TransformToAncestor(root).Transform(new Point(0, 0));
        var area = new Rect(topLeft, new Size(button.ActualWidth, button.ActualHeight));
        Assert.True(bounds.Contains(area), $"{name} lies outside the 900x600 area.");
    }

    private static T? FindByName<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && string.Equals(element.Name, name, StringComparison.Ordinal))
            {
                return element;
            }

            var found = FindByName<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Gives views an <see cref="Application"/> with the theme and control dictionaries App.xaml merges.</summary>
    private static void EnsureApplicationResources()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ThemesDictionary());
        application.Resources.MergedDictionaries.Add(new ControlsDictionary());
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/NexGrades.App;component/Shell/ShellControlStyles.xaml") });
    }
}
