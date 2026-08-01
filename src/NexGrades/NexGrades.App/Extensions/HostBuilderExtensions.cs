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

        // Configuration
        //_ = services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));

        services.AddSingleton<IUserService, UserService>();
        services.AddTransient<IFileSystemDialogService, WindowsFileSystemDialogService>();
        services.AddSingleton<ISnackbarService, SnackbarService>();
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
        var dbCon = GetAppDbConnectionString(builder.Configuration, builder.Environment);
        builder.Services.AddPooledDbContextFactory<AppDbContext>((serviceProvider, options) =>
            options
                .UseSqlite(dbCon)
                .AddInterceptors(serviceProvider.GetRequiredService<ClosedYearAuditInterceptor>()));
        builder.Services.AddTransient<DatabaseMigrationService>();

        return builder;
    }

    private static string GetAppDbConnectionString(IConfiguration configuration, IHostEnvironment env)
    {
        var dbConnection = env.IsDevelopment() ? configuration.GetConnectionString("sqlite") : configuration.GetValue<string>("sqlite");
        return dbConnection ?? throw new InvalidOperationException("DbConnection must not be null");
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
