using Wpf.Ui.Abstractions;

namespace NexGrades.App.DependencyModel;

public class DependencyInjectionNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    /// <inheritdoc />
    public object? GetPage(Type pageType)
    {
        return serviceProvider.GetService(pageType);
    }
}