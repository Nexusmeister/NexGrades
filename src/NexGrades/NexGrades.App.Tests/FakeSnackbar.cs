using System.Reflection;
using Wpf.Ui;

namespace NexGrades.App.Tests;

/// <summary>
/// An <see cref="ISnackbarService"/> built on <see cref="DispatchProxy"/> that records the title of every
/// <c>Show</c> call and does nothing else.
/// </summary>
public class FakeSnackbar : DispatchProxy
{
    private readonly List<string> _titles = [];

    /// <summary>The titles passed to <c>Show</c>, in call order.</summary>
    public IReadOnlyList<string> Titles
    {
        get
        {
            lock (_titles)
            {
                return [.. _titles];
            }
        }
    }

    public static (ISnackbarService Service, FakeSnackbar Fake) Create()
    {
        var proxy = Create<ISnackbarService, FakeSnackbar>();
        return (proxy, (FakeSnackbar)(object)proxy);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        if (targetMethod.Name == "Show" && args is { Length: > 0 } && args[0] is string title)
        {
            lock (_titles)
            {
                _titles.Add(title);
            }
        }

        var returnType = targetMethod.ReturnType;
        return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
    }
}
