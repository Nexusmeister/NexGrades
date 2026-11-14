using System.Reflection;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests;

/// <summary>
/// An <see cref="IContentDialogService"/> built on <see cref="DispatchProxy"/> that shows nothing, counts how often
/// a dialog was requested and answers every dialog with <see cref="Answer"/>.
/// </summary>
public class FakeContentDialogService : DispatchProxy
{
    private int _shown;

    /// <summary>The button the fake user presses in every dialog.</summary>
    public ContentDialogResult Answer { get; set; } = ContentDialogResult.None;

    /// <summary>How many dialogs were requested.</summary>
    public int Shown => Volatile.Read(ref _shown);

    public static (IContentDialogService Service, FakeContentDialogService Fake) Create(ContentDialogResult answer)
    {
        var proxy = Create<IContentDialogService, FakeContentDialogService>();
        var fake = (FakeContentDialogService)(object)proxy;
        fake.Answer = answer;
        return (proxy, fake);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(Task<ContentDialogResult>))
        {
            Interlocked.Increment(ref _shown);
            return Task.FromResult(Answer);
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
    }
}
