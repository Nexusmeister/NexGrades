using System.Windows;
using NexGrades.App.Theming;

namespace NexGrades.App.Tests.TestSupport;

/// <summary>An <see cref="IThemeApplier"/> that records what it was asked to show.</summary>
internal sealed class FakeThemeApplier : IThemeApplier
{
    public List<ThemePreference> Applied { get; } = [];

    public List<Window> AttachedWindows { get; } = [];

    public void Apply(ThemePreference preference) => Applied.Add(preference);

    public void AttachWindow(Window window) => AttachedWindows.Add(window);
}
