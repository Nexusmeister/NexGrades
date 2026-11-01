using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NexGrades.App.Core;
using NexGrades.App.Extensions;
using NexGrades.App.Features.Home;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace NexGrades.App.Tests;

/// <summary>
/// Guards the page/view-model lifetime rule: the overview roots are singletons, every other page (detail and
/// edit pages) is transient, and a page and its view model always share a lifetime. Also checks that
/// <see cref="PageNavigator"/> hands a parameter over exactly once.
/// </summary>
public class PageLifetimeTests
{
    private static readonly HashSet<string> _rootPages = new(StringComparer.Ordinal)
    {
        "HomePage",
        "ClassesOverviewPage",
        "StudentsOverviewPage",
        "SubjectsOverviewPage",
        "SettingsPage",
    };

    [Fact]
    public void AddFrontendServices_RegistersPageAndViewModel_WithSameLifetime()
    {
        var services = BuildServices();

        var pages = FindPageTypes();
        Assert.NotEmpty(pages);
        foreach (var (page, viewModel) in pages)
        {
            var pageLifetime = LifetimeOf(services, page);
            var viewModelLifetime = LifetimeOf(services, viewModel);

            Assert.True(pageLifetime == viewModelLifetime, $"{page.Name} is {pageLifetime} but {viewModel.Name} is {viewModelLifetime}.");
        }
    }

    [Fact]
    public void AddFrontendServices_RegistersRootPages_AsSingleton()
    {
        var services = BuildServices();

        // HomePage is a plain Page without INavigableView, so it is checked by name below.
        var roots = FindPageTypes().Where(p => _rootPages.Contains(p.Page.Name)).ToList();

        Assert.Equal(_rootPages.Count - 1, roots.Count);
        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, typeof(HomePage)));
        Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, typeof(HomeViewModel)));
        foreach (var (page, viewModel) in roots)
        {
            Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, page));
            Assert.Equal(ServiceLifetime.Singleton, LifetimeOf(services, viewModel));
        }
    }

    [Fact]
    public void AddFrontendServices_RegistersNonRootPages_AsTransient()
    {
        var services = BuildServices();

        var others = FindPageTypes().Where(p => !_rootPages.Contains(p.Page.Name)).ToList();

        Assert.NotEmpty(others);
        foreach (var (page, viewModel) in others)
        {
            Assert.Equal(ServiceLifetime.Transient, LifetimeOf(services, page));
            Assert.Equal(ServiceLifetime.Transient, LifetimeOf(services, viewModel));
        }
    }

    [Theory]
    [InlineData("ClassEditRequest")]
    [InlineData("StudentEditRequest")]
    public void AppAssembly_HasNoType_ForRemovedEditRequest(string typeName)
    {
        Assert.DoesNotContain(typeof(App).Assembly.GetTypes(), t => t.Name == typeName);
    }

    [Fact]
    public void NavigateWithHierarchy_DeliversParameterOnce_ThenNullOnLaterVisit()
    {
        var received = new List<object?>();
        var navigator = new PageNavigator(FakeNavigation.Create(() => new FakeViewModel(received)));
        var parameter = new object();

        Assert.True(navigator.NavigateWithHierarchy<FakePage>(parameter));
        Assert.True(navigator.NavigateWithHierarchy<FakePage>());

        Assert.Equal(2, received.Count);
        Assert.Same(parameter, received[0]);
        Assert.Null(received[1]);
    }

    [Fact]
    public void NavigateWithHierarchy_LeavesNoStaleParameter_WhenNavigationFails()
    {
        var received = new List<object?>();
        var failing = new PageNavigator(FakeNavigation.Create(() => new FakeViewModel(received), result: false));
        var working = new PageNavigator(FakeNavigation.Create(() => new FakeViewModel(received)));

        Assert.False(failing.NavigateWithHierarchy<FakePage>(new object()));
        Assert.True(working.NavigateWithHierarchy<FakePage>());

        Assert.Single(received);
        Assert.Null(received[0]);
    }

    [Fact]
    public void NavigateWithHierarchy_LeavesNoStaleParameter_WhenNavigationThrows()
    {
        var received = new List<object?>();
        var failing = new PageNavigator(FakeNavigation.Create(() => new FakeViewModel(received), throws: true));
        var working = new PageNavigator(FakeNavigation.Create(() => new FakeViewModel(received)));

        _ = Assert.Throws<InvalidOperationException>(() => failing.NavigateWithHierarchy<FakePage>(new object()));
        Assert.True(working.NavigateWithHierarchy<FakePage>());

        Assert.Single(received);
        Assert.Null(received[0]);
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        _ = services.AddFrontendServices();

        return services;
    }

    private static ServiceLifetime LifetimeOf(IServiceCollection services, Type type)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == type);
        Assert.True(descriptor is not null, $"{type.Name} is not registered.");

        return descriptor.Lifetime;
    }

    private static (Type Page, Type ViewModel)[] FindPageTypes()
    {
        return [.. typeof(App).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true })
            .Select(t => (Page: t, Navigable: t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INavigableView<>))))
            .Where(x => x.Navigable is not null)
            .Select(x => (x.Page, x.Navigable!.GetGenericArguments()[0]))];
    }

    /// <summary>Stands in for a page; only its <see cref="INavigableView{T}"/> shape matters to the navigator.</summary>
    public sealed class FakePage : INavigableView<FakeViewModel>
    {
        public FakeViewModel ViewModel { get; } = null!;
    }

    /// <summary>Records the parameter that reaches <c>OnNavigatedToAsync(object?)</c>.</summary>
    public sealed class FakeViewModel(ICollection<object?> received) : ViewModel
    {
        protected override Task OnNavigatedToAsync(object? parameter)
        {
            received.Add(parameter);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A minimal <see cref="INavigationService"/> built on <see cref="DispatchProxy"/>: any navigate-to-type call
    /// constructs the view model the way the page provider would and runs its navigated-to callback.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "INavigationService.Navigate is synchronous, so the fake must run the async callback to completion.")]
    public class FakeNavigation : DispatchProxy
    {
        private Func<ViewModel> _createViewModel = null!;
        private bool _result = true;
        private bool _throws;

        public static INavigationService Create(Func<ViewModel> createViewModel, bool result = true, bool throws = false)
        {
            var proxy = Create<INavigationService, FakeNavigation>();
            var fake = (FakeNavigation)(object)proxy;
            fake._createViewModel = createViewModel;
            fake._result = result;
            fake._throws = throws;

            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null || args is null || !args.OfType<Type>().Any())
            {
                return targetMethod?.ReturnType == typeof(bool) ? _result : null;
            }

            if (_throws)
            {
                throw new InvalidOperationException("Navigation failed.");
            }

            if (_result)
            {
                _createViewModel().OnNavigatedToAsync().GetAwaiter().GetResult();
            }

            return targetMethod.ReturnType == typeof(bool) ? _result : null;
        }
    }
}
