using System.Reflection;
using Wpf.Ui;

namespace NexGrades.App.Tests;

/// <summary>An <see cref="INavigationService"/> built on <see cref="DispatchProxy"/> that does nothing and returns defaults.</summary>
public class FakeNavigationService : DispatchProxy
{
    public static INavigationService Create() => Create<INavigationService, FakeNavigationService>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod is { ReturnType: { IsValueType: true } type } && type != typeof(void) ? Activator.CreateInstance(type) : null;
}
