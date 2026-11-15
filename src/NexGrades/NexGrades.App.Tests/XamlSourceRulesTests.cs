using System.IO;
using System.Xml.Linq;

namespace NexGrades.App.Tests;

/// <summary>
/// Scans the App's XAML sources for mistakes that only show at runtime. WPF-UI replaces the style of a
/// <c>ui:TextBlock</c>, so a <c>Visibility</c> setter or trigger in its own style never applies; bind
/// <c>Visibility</c> directly instead. Not covered: styles applied by type (an implicit style or a keyed style
/// reached only through <c>TargetType</c>), and styles set from code or through a <c>BasedOn</c> chain.
/// </summary>
public class XamlSourceRulesTests
{
    private static readonly XNamespace _presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace _wpfUi = "http://schemas.lepo.co/wpfui/2022/xaml";

    [Fact]
    public void XamlSources_AreFound()
    {
        Assert.NotEmpty(LoadXamlFiles());
    }

    [Fact]
    public void UiTextBlock_HasNoVisibilityInItsStyle_InAnyXamlFile()
    {
        var offenders = FindUiTextBlockVisibilityOffenders(LoadXamlFiles());

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void IsUiTextBlock_MatchesAtLeastOneElement_InAppXamlFiles()
    {
        var count = LoadXamlFiles().Sum(f => f.Document.Descendants().Count(IsUiTextBlock));

        Assert.True(count > 0, "No ui:TextBlock was found, so the Visibility rule would pass vacuously.");
    }

    [Fact]
    public void FindUiTextBlockVisibilityOffenders_Flags_InlineStyleWithVisibilitySetter()
    {
        var doc = Parse("""
            <ui:TextBlock>
                <ui:TextBlock.Style>
                    <Style TargetType="ui:TextBlock">
                        <Setter Property="Visibility" Value="Collapsed"/>
                    </Style>
                </ui:TextBlock.Style>
            </ui:TextBlock>
            """);

        Assert.Single(FindUiTextBlockVisibilityOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public void FindUiTextBlockVisibilityOffenders_Flags_InlineStyleWithVisibilityDataTrigger()
    {
        var doc = Parse("""
            <ui:TextBlock>
                <ui:TextBlock.Style>
                    <Style TargetType="ui:TextBlock">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding X}" Value="True">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </ui:TextBlock.Style>
            </ui:TextBlock>
            """);

        Assert.NotEmpty(FindUiTextBlockVisibilityOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public void FindUiTextBlockVisibilityOffenders_Flags_KeyedStyleWithVisibility()
    {
        var doc = Parse("""
            <Grid>
                <Grid.Resources>
                    <Style x:Key="Hide" TargetType="ui:TextBlock">
                        <Setter Property="Visibility" Value="Collapsed"/>
                    </Style>
                </Grid.Resources>
                <ui:TextBlock Style="{StaticResource Hide}"/>
            </Grid>
            """);

        Assert.Single(FindUiTextBlockVisibilityOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public void FindUiTextBlockVisibilityOffenders_FlagsNothing_ForStyleWithoutVisibility()
    {
        var doc = Parse("""
            <ui:TextBlock Visibility="{Binding V}">
                <ui:TextBlock.Style>
                    <Style TargetType="ui:TextBlock">
                        <Setter Property="Foreground" Value="Red"/>
                    </Style>
                </ui:TextBlock.Style>
            </ui:TextBlock>
            """);

        Assert.Empty(FindUiTextBlockVisibilityOffenders([("clean.xaml", doc)]));
    }

    [Fact]
    public void FindUiTextBlockVisibilityOffenders_FlagsNothing_ForPlainWpfTextBlock()
    {
        var doc = Parse("""
            <TextBlock>
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Visibility" Value="Collapsed"/>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
            """);

        Assert.Empty(FindUiTextBlockVisibilityOffenders([("plain.xaml", doc)]));
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

    private static XDocument Parse(string body) =>
        XDocument.Parse(
            $"""<Root xmlns="{_presentation}" xmlns:x="{_xaml}" xmlns:ui="{_wpfUi}">{body}</Root>""");

    private static List<string> FindUiTextBlockVisibilityOffenders(IReadOnlyList<(string Path, XDocument Document)> files)
    {
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

                var key = ExtractResourceKey((string?)textBlock.Attribute("Style"));
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

        return offenders;
    }

    private static bool IsUiTextBlock(XElement element) =>
        element.Name == _wpfUi + "TextBlock";

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
