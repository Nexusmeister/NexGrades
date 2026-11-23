using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Grades;
using NexGrades.App.Logging;
using NexGrades.App.Storage;
using Wpf.Ui.Markup;

namespace NexGrades.App.Tests;

/// <summary>
/// Instantiates the real column header template of the grade sheet page, so a trigger or style that does not
/// take effect is caught. The "Type 1-6 to override" hint belongs to the final grade column only.
/// </summary>
[Collection(SharedTelemetryState.Name)]
public class GradeSheetColumnHeaderTemplateTests
{
    private const string HintText = "Type 1–6 to override";

    [Theory]
    [InlineData(GradeSheetColumnKind.Student, Visibility.Collapsed)]
    [InlineData(GradeSheetColumnKind.Assessment, Visibility.Collapsed)]
    [InlineData(GradeSheetColumnKind.OtherGrades, Visibility.Collapsed)]
    [InlineData(GradeSheetColumnKind.BucketAverage, Visibility.Collapsed)]
    [InlineData(GradeSheetColumnKind.ProposedFinalGrade, Visibility.Collapsed)]
    [InlineData(GradeSheetColumnKind.FinalGrade, Visibility.Visible)]
    public void HeaderTemplate_ShowsOverrideHint_OnlyForFinalGradeColumn(GradeSheetColumnKind kind, Visibility expected)
    {
        SharedStaThread.Run(() =>
        {
            EnsureApplicationResources();

            var dbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-header-test-{Guid.NewGuid():N}.db");
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

            var page = host.Services.GetRequiredService<GradeSheetPage>();
            var template = (DataTemplate)page.Resources[DataGridColumnsBehavior.HeaderTemplateKey];
            var column = new GradeSheetColumn { Kind = kind, Header = "Header" };

            var presenter = new ContentPresenter { Content = column, ContentTemplate = template };
            var window = new Window { Content = presenter, Width = 300, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                presenter.ApplyTemplate();
                presenter.UpdateLayout();

                var hint = FindDescendants<TextBlock>(presenter).Single(t => t.Text == HintText);
                Assert.Equal(expected, hint.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }

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
