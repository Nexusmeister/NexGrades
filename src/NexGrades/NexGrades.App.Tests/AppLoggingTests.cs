using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Classes;
using NexGrades.App.Infrastructure;
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

    [Fact]
    public void AddAppLogging_DoesNotThrowAndHostStillLogs_WhenPathsThrow()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production, ContentRootPath = AppContext.BaseDirectory });

        var exception = Record.Exception(() => builder.AddAppLogging(new FileLoggingTests.ThrowingPaths()));

        Assert.Null(exception);
        using var host = builder.Build();
        host.Services.GetRequiredService<ILogger<AppLoggingTests>>().LogError("still works");
        Assert.DoesNotContain(host.Services.GetServices<ILoggerProvider>(), p => p.GetType().FullName!.Contains("Serilog", StringComparison.Ordinal));
    }

    [Fact]
    public void AddAppLogging_StillWritesLogFileWithDefaults_WhenRetainedFileCountIsNotANumber()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production, ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:File:RetainedFileCount"] = "abc" });

        var exception = Record.Exception(() => builder.AddAppLogging(Paths));

        Assert.Null(exception);
        using (var host = builder.Build())
        {
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NexGrades.Domain.Thing").LogWarning("ZZ-BAD-SETTING");
        }

        Assert.Contains("ZZ-BAD-SETTING", ReadLog(), StringComparison.Ordinal);
    }

    private sealed class RecordingLogger(string category, List<(string Category, string Message)> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            messages.Add((category, formatter(state, exception)));
        }
    }

    [ProviderAlias("OpenTelemetry")]
    private sealed class FakeOpenTelemetryProvider : ILoggerProvider
    {
        public List<(string Category, string Message)> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Messages);

        public void Dispose()
        {
        }
    }

    [Fact]
    public void LogStartup_KeepsPathsLineOutOfOpenTelemetryProvider_ButWritesItToTheFile()
    {
        var builder = CreateBuilder(Environments.Production);
        var telemetry = new FakeOpenTelemetryProvider();
        _ = builder.Logging.AddProvider(telemetry);
        var databasePath = Path.Combine(_folder.Path, "ZZ-pupils.db");
        using (var host = builder.Build())
        {
            var factory = host.Services.GetRequiredService<ILoggerFactory>();
            StartupDiagnostics.LogStartup(
                factory.CreateLogger("NexGrades.App"),
                factory,
                host.Services.GetRequiredService<IHostEnvironment>(),
                new DatabaseLocation($"Data Source={databasePath}", IsOverride: false),
                Paths);
        }

        Assert.Contains(telemetry.Messages, m => m.Message.StartsWith("NexGrades ", StringComparison.Ordinal));
        Assert.DoesNotContain(telemetry.Messages, m => m.Message.Contains("ZZ-pupils.db", StringComparison.Ordinal) || m.Message.Contains(_folder.Path, StringComparison.Ordinal));
        var text = ReadLog();
        Assert.Contains("ZZ-pupils.db", text, StringComparison.Ordinal);
        Assert.Contains("log folder", text, StringComparison.Ordinal);
    }
}
