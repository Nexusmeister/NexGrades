using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using NexGrades.App.ViewModels;
using Wpf.Ui.Abstractions;

namespace NexGrades.App.DependencyModel;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransientFromNamespace(
        this IServiceCollection services,
        string namespaceName,
        params Assembly[] assemblies
    )
    {
        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<Type> types = assembly
                .GetTypes()
                .Where(x =>
                    x.IsClass
                    && x.Namespace!.StartsWith(namespaceName, StringComparison.InvariantCultureIgnoreCase)
                );

            foreach (Type? type in types)
            {
                if (services.All(x => x.ServiceType != type))
                {
                    if (type == typeof(ViewModel))
                    {
                        continue;
                    }

                    _ = services.AddTransient(type);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Adds the services necessary for page navigation within a WPF UI NavigationView.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddNavigationViewPageProvider(this IServiceCollection services)
    {
        _ = services.AddSingleton<
            INavigationViewPageProvider,
            DependencyInjectionNavigationViewPageProvider
        >();

        return services;
    }
}