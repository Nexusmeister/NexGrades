using System.IO;
using System.Xml.Linq;

namespace NexGrades.App.Tests;

/// <summary>
/// Scans the App's XAML sources for mistakes that only show at runtime. WPF-UI replaces the style of a
/// <c>ui:TextBlock</c>, so a <c>Visibility</c> setter or trigger in its own style never applies; bind
/// <c>Visibility</c> directly instead.
/// </summary>
public class XamlSourceRulesTests
{
    private static readonly XNamespace _presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void XamlSources_AreFound()
    {
        Assert.NotEmpty(LoadXamlFiles());
    }

    [Fact]
    public void UiTextBlock_HasNoVisibilityInItsStyle_InAnyXamlFile()
    {
        var files = LoadXamlFiles();
        var offenders = new List<string>();

        foreach (var (path, doc) in files)
        {
            foreach (var textBlock in doc.Descendants().Where(IsUiTextBlock))
            {
                var inline = textBlock.Elements()
                    .Where(e => e.Name.LocalName == "TextBlock.Style")
                    .SelectMany(e => e.Descendants())
                    .Where(e => e.Name.LocalName == "Style")
                    .ToList();
                if (inline.Any(StyleTouchesVisibility))
                {
                    offenders.Add($"{path}: inline style of ui:TextBlock");
                }

                var reference = (string?)textBlock.Attribute("Style");
                var key = ExtractResourceKey(reference);
                if (key is null)
                {
                    continue;
                }

                var styles = files.SelectMany(f => f.Document.Descendants())
                    .Where(e => e.Name == _presentation + "Style" && (string?)e.Attribute(_xaml + "Key") == key);
                if (styles.Any(StyleTouchesVisibility))
                {
                    offenders.Add($"{path}: ui:TextBlock uses style '{key}' that sets Visibility");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void PageTitle_IsNotAnIdentifier_InAnyPage()
    {
        var offenders = new List<string>();

        foreach (var (path, doc) in LoadXamlFiles())
        {
            var root = doc.Root!;
            var className = (string?)root.Attribute(_xaml + "Class");
            var title = (string?)root.Attribute("Title");
            if (className is null || title is null)
            {
                continue;
            }

            var shortName = className[(className.LastIndexOf('.') + 1)..];
            if (string.Equals(title, shortName, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{path}: Title '{title}' equals the class name");
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static bool IsUiTextBlock(XElement element) =>
        element.Name.LocalName == "TextBlock"
        && element.Name.Namespace != _presentation
        && element.Name.NamespaceName.Contains("Wpf.Ui", StringComparison.OrdinalIgnoreCase);

    private static bool StyleTouchesVisibility(XElement style) =>
        style.Descendants().Any(e =>
            (e.Name.LocalName is "Setter" or "Trigger" or "DataTrigger" or "MultiDataTrigger" or "MultiTrigger")
            && ((string?)e.Attribute("Property") == "Visibility"
                || (e.Name.LocalName == "Setter" && (string?)e.Attribute("Property") == "UIElement.Visibility")));

    private static string? ExtractResourceKey(string? reference)
    {
        if (reference is null || !reference.StartsWith('{') || !reference.Contains("Resource", StringComparison.Ordinal))
        {
            return null;
        }

        var inner = reference.Trim('{', '}').Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return inner.Length == 2 ? inner[1].Trim() : null;
    }

    private static List<(string Path, XDocument Document)> LoadXamlFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NexGrades.App")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return [.. Directory.EnumerateFiles(Path.Combine(dir.FullName, "NexGrades.App"), "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(p => (Path.GetRelativePath(dir.FullName, p), XDocument.Load(p)))];
    }
}
