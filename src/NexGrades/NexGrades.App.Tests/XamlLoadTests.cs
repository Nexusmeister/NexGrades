using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Home;
using NexGrades.App.Features.Students;
using NexGrades.App.Logging;
using NexGrades.App.Shell;
using NexGrades.App.Storage;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards against XAML that compiles but throws a <see cref="System.Windows.Markup.XamlParseException"/> when
/// the view is loaded (for example a <c>DynamicResource</c> on <c>BasedOn</c>). The DI tests never run
/// <c>InitializeComponent</c> for the dialogs and controls, and a build does not validate resource references,
/// so each view is created here on an STA thread with the WPF-UI theme merged, as App.xaml does.
/// </summary>
[Collection(SharedTelemetryState.Name)]
public class XamlLoadTests
{
    private static readonly Type[] _otherViews =
    [
        typeof(HomePage),
        typeof(MainWindow),
        typeof(YearLockBar),
        typeof(UnlockYearDialog),
        typeof(ExportNotesDialog),
    ];

    public static TheoryData<string> ViewNames()
    {
        var data = new TheoryData<string>();
        foreach (var type in FindViewTypes())
        {
            data.Add(type.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ViewNames))]
    public void InitializeComponent_LoadsXaml_ForView(string viewName)
    {
        var viewType = FindViewTypes().Single(t => t.Name == viewName);

        SharedStaThread.Run(() =>
        {
            EnsureApplicationResources();

            var dbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-xaml-test-{Guid.NewGuid():N}.db");
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:sqlite"] = $"Data Source={dbPath}",
            });
            using var logFolder = new TempFolder();
            builder.AddAppLogging(new AppDataPaths(logFolder.Path));
            builder.Services.AddShell().AddDomain().AddFeatures();
            builder.AddDatabase();

            using var host = builder.Build();

            var view = CreateView(host.Services, viewType);
            try
            {
                Assert.NotNull(view);
                RealizeResources(view);
            }
            finally
            {
                (view as Window)?.Close();
            }
        });
    }

    /// <summary>
    /// Resources declared in XAML are parsed lazily, on first lookup. Reading every value forces the parse, so a
    /// bad resource (such as a <c>DynamicResource</c> on <c>BasedOn</c>) fails here even if nothing uses it yet.
    /// </summary>
    private static void RealizeResources(object view)
    {
        if (view is FrameworkElement element)
        {
            RealizeDictionary(element.Resources);
        }

        if (view is DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<object>())
            {
                RealizeResources(child);
            }
        }
    }

    private static void RealizeDictionary(ResourceDictionary dictionary)
    {
        foreach (var key in dictionary.Keys)
        {
            _ = dictionary[key];
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            RealizeDictionary(merged);
        }
    }

    private static Type[] FindViewTypes()
    {
        var pages = typeof(App).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INavigableView<>)));

        return [.. pages.Concat(_otherViews).Distinct().OrderBy(t => t.Name, StringComparer.Ordinal)];
    }

    private static object CreateView(IServiceProvider services, Type viewType)
    {
        if (viewType == typeof(MainWindow))
        {
            return services.GetRequiredService<Wpf.Ui.INavigationWindow>();
        }

        if (viewType == typeof(YearLockBar))
        {
            return new YearLockBar();
        }

        if (viewType == typeof(UnlockYearDialog))
        {
            return new UnlockYearDialog(new ContentDialogHost(), new UnlockYearDialogViewModel("test", new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local))));
        }

        if (viewType == typeof(ExportNotesDialog))
        {
            return new ExportNotesDialog(new ContentDialogHost(), new ExportNotesDialogViewModel(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        }

        return services.GetRequiredService(viewType);
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
