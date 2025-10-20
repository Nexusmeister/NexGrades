using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // Service containing navigation, same as INavigationWindow... but without window


        // Main window with navigation



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

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var dbCon = GetAppDbConnectionString(configuration);
        services.AddDbContextFactory<AppDbContext>(opt => opt.UseSqlite(dbCon));
        services.AddTransient<DatabaseMigrationService>();

        return services;
    }

    private static string GetAppDbConnectionString(IConfiguration configuration) => configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DbConnection must not be null");

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