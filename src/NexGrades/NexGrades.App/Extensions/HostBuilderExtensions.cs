using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NexGrades.App.Features.Classes;
using NexGrades.App.Features.Home;
using NexGrades.App.Features.Settings;
using NexGrades.App.Features.Students;
using NexGrades.App.Features.Subjects;
using NexGrades.App.Infrastructure;
using NexGrades.App.Shell;
using NexGrades.Common.Services;
using NexGrades.Common.Storage;
using NexGrades.Data;
using NexGrades.Data.Services;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using SettingsViewModel = NexGrades.App.Features.Settings.SettingsViewModel;
using StudentsOverviewViewModel = NexGrades.App.Features.Students.StudentsOverviewViewModel;

namespace NexGrades.App.Extensions;

public static class HostBuilderExtensions
{
    public static IServiceCollection AddGradeServices(this IServiceCollection services)
    {
        _ = services
            .AddNavigationViewPageProvider()
            .AddHostedService<ApplicationHostService>()
            .AddSingleton<IThemeService, ThemeService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<INavigationWindow, MainWindow>()
            .AddSingleton<MainWindowViewModel>();

        // TaskBar manipulation
        //_ = services.AddSingleton<ITaskBarService, TaskBarService>();

        // Domain: time, closed-year auditing, and the business services ported from the GradeTracker
        // architecture spike. IClock/IUnlockReasonStore are singletons (see their remarks); the interceptor
        // is also a singleton — EF Core shares one interceptor instance across every DbContext it creates and
        // the interceptor itself is stateless per-context (state lives in a ConditionalWeakTable keyed by
        // DbContext instance).
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUnlockReasonStore, UnlockReasonStore>();
        services.AddSingleton<ClosedYearAuditInterceptor>();
        services.AddTransient<EnrollmentService>();
        services.AddTransient<GradeService>();
        services.AddTransient<AverageService>();
        services.AddTransient<RolloverService>();
        services.AddTransient<YearCloseService>();
        services.AddTransient<UnlockService>();
        services.AddTransient<NoteService>();

        // Configuration
        //_ = services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));

        services.AddSingleton<IUserService, UserService>();
        services.AddTransient<IFileSystemDialogService, WindowsFileSystemDialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        return services;
    }

    public static IServiceCollection AddFrontendServices(this IServiceCollection services)
    {
        // Views and ViewModels
        return services
            .AddViews()
            .AddViewModels();

    }

    public static HostApplicationBuilder AddDatabase(this HostApplicationBuilder builder)
    {
        var paths = AppDataPaths.CreateDefault();
        var location = ResolveDatabaseLocation(builder.Configuration, builder.Environment, paths);
        builder.Services.AddSingleton<IAppDataPaths>(paths);
        builder.Services.AddSingleton(location);
        builder.Services.AddSingleton<LegacyDatabaseRelocator>();
        builder.Services.AddPooledDbContextFactory<AppDbContext>((serviceProvider, options) =>
            options
                .UseSqlite(location.ConnectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<ClosedYearAuditInterceptor>()));
        builder.Services.AddTransient<DatabaseMigrationService>();

        return builder;
    }

    /// <summary>
    /// A configured connection string wins (Development: <c>ConnectionStrings:sqlite</c> from Aspire; otherwise
    /// the <c>sqlite</c> key). With none configured, the database goes to the fixed per-user folder.
    /// </summary>
    private static DatabaseLocation ResolveDatabaseLocation(IConfiguration configuration, IHostEnvironment env, IAppDataPaths paths)
    {
        var configured = env.IsDevelopment() ? configuration.GetConnectionString("sqlite") : configuration.GetValue<string>("sqlite");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new DatabaseLocation(configured, IsOverride: true);
        }

        // Never fall back to the real per-user database in Development: a dev build could migrate the teacher's data.
        if (env.IsDevelopment())
        {
            throw new InvalidOperationException("ConnectionStrings:sqlite must be set in Development (Aspire or user secrets).");
        }

        var connection = new SqliteConnectionStringBuilder { DataSource = paths.DatabaseFilePath };
        return new DatabaseLocation(connection.ToString(), IsOverride: false);
    }

    private static IServiceCollection AddViews(this IServiceCollection services)
    {
        return services.AddSingleton<HomePage>()
            .AddSingleton<ClassesOverviewPage>()
            .AddSingleton<StudentsOverviewPage>()
            .AddSingleton<SubjectsOverviewPage>()
            .AddTransient<StudentPage>()
            .AddTransient<SubjectDetailPage>()
            .AddTransient<ClassPage>()
            .AddSingleton<SettingsPage>();
    }

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        return services
            .AddSingleton<ClassEditRequest>()
            .AddSingleton<StudentEditRequest>()
            .AddSingleton<HomeViewModel>()
            .AddSingleton<ClassesOverviewViewModel>()
            .AddSingleton<StudentsOverviewViewModel>()
            .AddSingleton<SubjectsOverviewViewModel>()
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<SubjectDetailViewModel>()
            .AddTransient<StudentViewModel>()
            .AddTransient<ClassViewModel>();
    }
}
