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

    public MainWindowViewModel ViewModel { get; }

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

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    public void ShowWindow() => Show();

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

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        throw new NotSupportedException();
    }
}
