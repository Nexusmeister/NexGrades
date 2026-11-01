using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Core;

/// <summary>
/// Opens a page below the current one and hands a parameter to its view model, which receives it in
/// <see cref="ViewModel.OnNavigatedToAsync(object?)"/>. It replaces per-page "edit request" singletons.
/// </summary>
/// <param name="navigation">The WPF-UI navigation service that performs the navigation.</param>
public sealed class PageNavigator(INavigationService navigation)
{
    /// <summary>
    /// Navigates to <typeparamref name="TPage"/> keeping the back stack, passing <paramref name="parameter"/> to
    /// the page's view model. The pages set their own <c>DataContext</c>, so the data-context overload of
    /// <c>Navigate</c> cannot carry the value.
    /// </summary>
    /// <typeparam name="TPage">A page that implements <see cref="INavigableView{T}"/>.</typeparam>
    /// <param name="parameter">The value for the view model, or <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when the navigation happened.</returns>
    public bool NavigateWithHierarchy<TPage>(object? parameter = null)
    {
        var viewModelType = typeof(TPage).GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INavigableView<>))
            .GetGenericArguments()[0];

        NavigationParameters.Set(viewModelType, parameter);
        try
        {
            var navigated = navigation.NavigateWithHierarchy(typeof(TPage));
            if (!navigated)
            {
                _ = NavigationParameters.Take(viewModelType);
            }

            return navigated;
        }
        catch
        {
            _ = NavigationParameters.Take(viewModelType);
            throw;
        }
    }
}
