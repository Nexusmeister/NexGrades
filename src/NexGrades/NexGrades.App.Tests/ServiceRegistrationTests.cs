using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Extensions;
using NexGrades.App.Logging;
using NexGrades.Common.Storage;
using NexGrades.Data.Diagnostics;
using NexGrades.ServiceDefaults;
using Wpf.Ui.Markup;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards the DI graph the app is actually built with. The WPF navigation resolves pages and view models from
/// the root provider (there is no scope around a page), so anything they depend on must be resolvable from the
/// root: a dependency on a scoped service such as <c>AppDbContext</c> works by accident outside Development and
/// throws under Aspire/Development, where the host turns on scope validation.
/// </summary>
[Collection(SharedTelemetryState.Name)]
public class ServiceRegistrationTests
{
    [Fact]
    public void Build_ResolvesEveryAppService_FromTheRootProviderWithScopeValidation()
    {
        RunOnStaThread(() =>
        {
            EnsureApplicationResources();

            var dbPath = Path.Combine(Path.GetTempPath(), $"nexgrades-di-test-{Guid.NewGuid():N}.db");

            // Same composition as App.CreateHost, but in Development (which turns on ValidateScopes and
            // ValidateOnBuild) and with the connection string Aspire would normally supply.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development,
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:sqlite"] = $"Data Source={dbPath}",
            });
            // Logging first, as in App.CreateHost: it clears the providers, which would drop the OpenTelemetry one.
            using var logFolder = new TempFolder();
            builder.AddAppLogging(new AppDataPaths(logFolder.Path));
            builder.AddServiceDefaults(NexGradesTelemetry.Name);
            builder.Services.AddGradeServices().AddFrontendServices();
            builder.AddDatabase();

            var appServiceTypes = builder.Services
                .Where(d => d.ServiceType.Assembly == typeof(App).Assembly || d.ServiceType.Namespace?.StartsWith("NexGrades.Domain", StringComparison.Ordinal) == true)
                .Where(d => d.ServiceType != typeof(IHostedService))
                .Select(d => d.ServiceType)
                .Distinct()
                .ToList();

            using var host = builder.Build();

            Assert.NotEmpty(appServiceTypes);
            foreach (var serviceType in appServiceTypes)
            {
                Assert.NotNull(host.Services.GetRequiredService(serviceType));
            }
        });
    }

    /// <summary>
    /// Pages run <c>InitializeComponent</c> in their constructors, which needs an <see cref="Application"/> with
    /// the WPF-UI theme and control dictionaries that App.xaml normally merges.
    /// </summary>
    private static void EnsureApplicationResources()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ThemesDictionary());
        application.Resources.MergedDictionaries.Add(new ControlsDictionary());
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
}
