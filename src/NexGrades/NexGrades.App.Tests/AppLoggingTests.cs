using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Classes;
using NexGrades.App.Logging;
using NexGrades.Common.Storage;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Services;
using NexGrades.ServiceDefaults;

namespace NexGrades.App.Tests;

/// <summary>
/// Tests <c>AddAppLogging</c> against a real host builder with the shipped appsettings files. They share the
/// process-wide Serilog <c>Log.Logger</c>, so they run in the non-parallel collection.
/// </summary>
[Collection(SharedTelemetryState.Name)]
[SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Tests log a few lines to drive the pipeline; performance is irrelevant.")]
[SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Tests pass fixed messages.")]
[SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "Tests log fixed values once.")]
public sealed class AppLoggingTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose()
    {
        Serilog.Log.CloseAndFlush();
        _folder.Dispose();
    }

    private AppDataPaths Paths => new(_folder.Path);

    private HostApplicationBuilder CreateBuilder(string environment, AppDataPaths? paths = null)
    {
        // Settings files from the App sit in the test output folder; the content root makes the host read them.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ContentRootPath = AppContext.BaseDirectory,
        });
        _ = builder.AddAppLogging(paths ?? Paths);
        return builder;
    }

    private string ReadLog()
    {
        var files = Directory.GetFiles(Paths.LogDirectory, "nexgrades-*.log");
        return string.Concat(files.Select(File.ReadAllText));
    }

    private string LogThroughPipeline(string environment, params (string Category, LogLevel Level, string Message)[] lines)
    {
        var builder = CreateBuilder(environment);
        using (var host = builder.Build())
        {
            var factory = host.Services.GetRequiredService<ILoggerFactory>();
            foreach (var (category, level, message) in lines)
            {
                factory.CreateLogger(category).Log(level, "{Text}", message);
            }
        }

        return ReadLog();
    }

    [Fact]
    public void AddAppLogging_RemovesConsoleAndEventLogProviders_AndKeepsFileProvider()
    {
        var builder = CreateBuilder(Environments.Production);

        using var host = builder.Build();
        var names = host.Services.GetServices<ILoggerProvider>().Select(p => p.GetType().FullName ?? string.Empty).ToList();

        Assert.DoesNotContain(names, n => n.Contains("Console", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("EventLog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.Contains("Serilog", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAppLogging_AddsDebugProvider_OnlyInDevelopment()
    {
        using var development = CreateBuilder(Environments.Development).Build();
        using var production = CreateBuilder(Environments.Production).Build();

        Assert.Contains(development.Services.GetServices<ILoggerProvider>(), p => p.GetType().Name.Contains("Debug", StringComparison.Ordinal));
        Assert.DoesNotContain(production.Services.GetServices<ILoggerProvider>(), p => p.GetType().Name.Contains("Debug", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAppLogging_KeepsOpenTelemetryProvider_WhenAddedBeforeServiceDefaults()
    {
        var builder = CreateBuilder(Environments.Development);
        _ = builder.AddServiceDefaults(NexGradesTelemetry.Name);

        using var host = builder.Build();
        var names = host.Services.GetServices<ILoggerProvider>().Select(p => p.GetType().FullName ?? string.Empty).ToList();

        Assert.Contains(names, n => n.Contains("OpenTelemetry", StringComparison.Ordinal));
        Assert.Contains(names, n => n.Contains("Serilog", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Console", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("EventLog", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddAppLogging_DoesNotThrowAndOmitsFileProvider_WhenLogFolderIsBlocked()
    {
        var blocked = new AppDataPaths(Path.Combine(_folder.Path, "blocked"));
        File.WriteAllText(blocked.DataDirectory, "a file where the data folder should be");

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production, ContentRootPath = AppContext.BaseDirectory });
        var exception = Record.Exception(() => builder.AddAppLogging(blocked));
        Assert.Null(exception);
        _ = builder.AddServiceDefaults(NexGradesTelemetry.Name);

        using var host = builder.Build();
        var names = host.Services.GetServices<ILoggerProvider>().Select(p => p.GetType().FullName ?? string.Empty).ToList();
        host.Services.GetRequiredService<ILogger<AppLoggingTests>>().LogError("still works");

        Assert.DoesNotContain(names, n => n.Contains("Serilog", StringComparison.Ordinal));
        Assert.Contains(names, n => n.Contains("OpenTelemetry", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAppLogging_Throws_ForNullBuilder() =>
        Assert.Throws<ArgumentNullException>(() => NexGrades.App.Logging.LoggingBuilderExtensions.AddAppLogging(null!, Paths));

    [Fact]
    public void Logging_WritesNexGradesInformation_AndDropsNexGradesDebug_InProduction()
    {
        var text = LogThroughPipeline(
            Environments.Production,
            ("NexGrades.Domain.Thing", LogLevel.Information, "ZZ-NG-INFO"),
            ("NexGrades.Domain.Thing", LogLevel.Debug, "ZZ-NG-DEBUG"));

        Assert.Contains("ZZ-NG-INFO", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-NG-DEBUG", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_WritesMicrosoftWarning_AndDropsMicrosoftInformation()
    {
        var text = LogThroughPipeline(
            Environments.Production,
            ("Microsoft.Something", LogLevel.Information, "ZZ-MS-INFO"),
            ("Microsoft.Something", LogLevel.Warning, "ZZ-MS-WARN"));

        Assert.Contains("ZZ-MS-WARN", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-MS-INFO", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_DropsInformationFromOtherCategories_ByDefaultWarningLevel()
    {
        var text = LogThroughPipeline(
            Environments.Production,
            ("Other.Library", LogLevel.Information, "ZZ-OTHER-INFO"),
            ("Other.Library", LogLevel.Warning, "ZZ-OTHER-WARN"));

        Assert.Contains("ZZ-OTHER-WARN", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-OTHER-INFO", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_WritesNexGradesDebugToFile_InDevelopment()
    {
        var text = LogThroughPipeline(
            Environments.Development,
            ("NexGrades.Domain.Thing", LogLevel.Debug, "ZZ-DEV-DEBUG"),
            ("Microsoft.Something", LogLevel.Information, "ZZ-DEV-MS-INFO"));

        Assert.Contains("ZZ-DEV-DEBUG", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ZZ-DEV-MS-INFO", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Logging_WritesLineInFilePattern_ThroughTheHost()
    {
        var text = LogThroughPipeline(Environments.Production, ("NexGrades.Domain.Thing", LogLevel.Warning, "hello"));

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[WRN\] NexGrades\.Domain\.Thing hello", text);
    }

    [Fact]
    public async Task DatabaseMigration_LogsNumericPendingCountLine_ThroughTheHostFile()
    {
        // End to end: the real service, MEL, the Serilog provider and the file formatter with an int property.
        var builder = CreateBuilder(Environments.Production);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["sqlite"] = $"Data Source={Path.Combine(_folder.Path, "app.db")}",
        });
        builder.Services.AddGradeServices().AddFrontendServices();
        builder.AddDatabase();
        using (var host = builder.Build())
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>().RunAsync();
        }

        var text = ReadLog();
        Assert.Contains("Database migrations applied", text, StringComparison.Ordinal);
        Assert.Matches(@"Checking database migrations; \d+ pending", text);
        Assert.Contains("Created an Active school year", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ResolvesLoggers_ForViewModelMigrationServiceAndCrashLogger()
    {
        var builder = CreateBuilder(Environments.Development);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:sqlite"] = $"Data Source={Path.Combine(_folder.Path, "app.db")}",
        });
        _ = builder.AddServiceDefaults(NexGradesTelemetry.Name);
        builder.Services.AddGradeServices().AddFrontendServices();
        builder.AddDatabase();

        using var host = builder.Build();

        Assert.NotNull(host.Services.GetRequiredService<ILogger<ClassViewModel>>());
        Assert.NotNull(host.Services.GetRequiredService<ILogger<DatabaseMigrationService>>());
        Assert.NotNull(host.Services.GetRequiredService<ILogger<UnhandledExceptionLogger>>());
        Assert.NotNull(host.Services.GetRequiredService<DatabaseMigrationService>());
    }
}
