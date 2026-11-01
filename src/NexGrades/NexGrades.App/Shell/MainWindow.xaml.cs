using System.Diagnostics;
using System.Windows;
using NexGrades.Data.Diagnostics;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace NexGrades.App.Shell;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : INavigationWindow
{
    private long _navigationStart;

    /// <summary>The view model of the window, which is also the window's <c>DataContext</c>.</summary>
    public MainWindowViewModel ViewModel { get; }

    /// <summary>
    /// Creates the window and makes it the navigation host. The snackbar and dialog services get their presenters
    /// from the XAML here, so they can show messages from any page.
    /// </summary>
    /// <param name="viewModel">The view model of the window.</param>
    /// <param name="navigationService">Receives this window's navigation control.</param>
    /// <param name="snackbarService">Receives this window's snackbar presenter.</param>
    /// <param name="contentDialogService">Receives this window's content dialog host.</param>
    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService, ISnackbarService snackbarService, IContentDialogService contentDialogService)
    {
        ViewModel = viewModel;
        DataContext = this;

        SystemThemeWatcher.Watch(this);

        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        contentDialogService.SetDialogHost(RootContentDialog);

        navigationService.SetNavigationControl(RootNavigation);

        // Measures how long it takes to resolve and show a page, not how long its view model needs to load data.
        RootNavigation.Navigating += (_, _) => _navigationStart = Stopwatch.GetTimestamp();
        RootNavigation.Navigated += OnNavigated;
    }

    private void OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (_navigationStart == 0)
        {
            return;
        }

        // Only the page type's name is a tag, never anything the page shows.
        NexGradesTelemetry.NavigationDuration.Record(
            Stopwatch.GetElapsedTime(_navigationStart).TotalSeconds,
            new KeyValuePair<string, object?>(NexGradesTelemetry.PageTag, args.Page.GetType().Name));
        _navigationStart = 0;
    }

    /// <summary>Returns the navigation control of this window.</summary>
    public INavigationView GetNavigation() => RootNavigation;

    /// <summary>Navigates to a page of the given type and returns whether the navigation succeeded.</summary>
    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    /// <summary>Gives the navigation control the provider that creates pages, so that pages are resolved from DI.</summary>
    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    /// <summary>Shows the window.</summary>
    public void ShowWindow() => Show();

    /// <summary>Closes the window. Closing it also shuts the application down, see <c>OnClosed</c>.</summary>
    public void CloseWindow() => Close();

    /// <summary>
    /// Raises the closed event.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Make sure that closing this window will begin the process of closing the application.
        Application.Current.Shutdown();
    }

    INavigationView INavigationWindow.GetNavigation()
    {
        throw new NotSupportedException();
    }

    /// <summary>Not supported. The window receives its dependencies through its constructor, so this always throws.</summary>
    /// <param name="serviceProvider">Not used.</param>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        throw new NotSupportedException();
    }
}
