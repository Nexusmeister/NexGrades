using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NexGrades.App.Tests.Composition;

/// <summary>
/// Scans the App's XAML sources for design system violations: a view that does not merge
/// <c>DesignSystem.xaml</c>, spacing that is not on the 4 px grid, and hard-coded hex colours.
/// </summary>
public static partial class DesignSystemRulesTests
{
    private static readonly XNamespace _presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace _wpfUi = "http://schemas.lepo.co/wpfui/2022/xaml";

    private static readonly HashSet<string> _viewRoots = ["Page", "UserControl", "ContentDialog", "FluentWindow"];

    [GeneratedRegex(@"(?<![\w&#])#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3,4})(?!\w)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HexColourPattern();

    [Fact]
    public static void EveryView_MergesDesignSystem_InItsResources()
    {
        var offenders = new List<string>();
        var views = 0;

        foreach (var (path, doc) in LoadXamlFiles())
        {
            if (!IsView(doc))
            {
                continue;
            }

            views++;
            var merges = doc.Descendants()
                .Any(e => e.Name.LocalName == "ResourceDictionary"
                    && ((string?)e.Attribute("Source"))?.EndsWith("DesignSystem/DesignSystem.xaml", StringComparison.OrdinalIgnoreCase) == true);
            if (!merges)
            {
                offenders.Add($"{path}: does not merge DesignSystem.xaml");
            }
        }

        Assert.True(views > 0, "No view was found, so the rule would pass vacuously.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void SpacingValues_AreMultiplesOfFour_InAllXamlFiles()
    {
        var offenders = FindSpacingOffenders(LoadXamlFiles());

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void XamlSources_HaveNoHexColours()
    {
        var offenders = FindHexColourOffenders(LoadXamlFiles());

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void FindSpacingOffenders_Flags_MarginAttributeOffTheGrid()
    {
        var doc = Parse("""<Grid Margin="5,0,0,0"/>""");

        Assert.Single(FindSpacingOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public static void FindSpacingOffenders_Flags_PaddingSetterOffTheGrid()
    {
        var doc = Parse("""<Style><Setter Property="Padding" Value="12,6"/></Style>""");

        Assert.Single(FindSpacingOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public static void FindSpacingOffenders_Flags_ThicknessResourceOffTheGrid()
    {
        var doc = Parse("""<Thickness x:Key="K">0,0,0,10</Thickness>""");

        Assert.Single(FindSpacingOffenders([("bad.xaml", doc)]));
    }

    [Theory]
    [InlineData("0,8")]
    [InlineData("24")]
    [InlineData("0,0,8,16")]
    [InlineData("{StaticResource Ng.Margin.Page}")]
    public static void FindSpacingOffenders_FlagsNothing_ForAllowedValue(string value)
    {
        var doc = Parse($"""<Grid Margin="{value}"/>""");

        Assert.Empty(FindSpacingOffenders([("clean.xaml", doc)]));
    }

    [Fact]
    public static void FindHexColourOffenders_Flags_HexColourAttribute()
    {
        var doc = Parse("""<Border Background="#FF3366"/>""");

        Assert.Single(FindHexColourOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public static void FindHexColourOffenders_FlagsNothing_ForNamesAndBindingsWithHash()
    {
        var doc = Parse("""<Border x:Name="Item#1" Tag="{Binding Path=Items[0], StringFormat=#,0}" ToolTip="Issue #12 of 100"/>""");

        Assert.Empty(FindHexColourOffenders([("clean.xaml", doc)]));
    }

    private static XDocument Parse(string body) =>
        XDocument.Parse(
            $"""<Root xmlns="{_presentation}" xmlns:x="{_xaml}" xmlns:ui="{_wpfUi}">{body}</Root>""");

    private static bool IsView(XDocument doc) =>
        doc.Root is { } root
        && _viewRoots.Contains(root.Name.LocalName)
        && (root.Name.Namespace == _presentation || root.Name.Namespace == _wpfUi);

    private static List<string> FindSpacingOffenders(IReadOnlyList<(string Path, XDocument Document)> files)
    {
        var offenders = new List<string>();

        foreach (var (path, doc) in files)
        {
            foreach (var element in doc.Descendants())
            {
                foreach (var attribute in element.Attributes().Where(a => a.Name.LocalName is "Margin" or "Padding"))
                {
                    Check(path, attribute.Name.LocalName, attribute.Value, offenders);
                }

                var property = (string?)element.Attribute("Property");
                if (element.Name.LocalName == "Setter" && property is "Margin" or "Padding")
                {
                    Check(path, $"Setter {property}", (string?)element.Attribute("Value"), offenders);
                }

                if (element.Name.LocalName == "Thickness" && !element.HasElements)
                {
                    Check(path, "Thickness", element.Value, offenders);
                }
            }
        }

        return offenders;
    }

    private static void Check(string path, string what, string? value, List<string> offenders)
    {
        if (string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith('{'))
        {
            return;
        }

        var parts = value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        var onGrid = parts.All(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && n % 4 == 0);
        if (!onGrid)
        {
            offenders.Add($"{path}: {what}=\"{value}\" is not a multiple of 4");
        }
    }

    private static List<string> FindHexColourOffenders(IReadOnlyList<(string Path, XDocument Document)> files)
    {
        var offenders = new List<string>();

        foreach (var (path, doc) in files)
        {
            var values = doc.Descendants().SelectMany(e => e.Attributes().Select(a => a.Value))
                .Concat(doc.DescendantNodes().OfType<XText>().Select(t => t.Value));
            offenders.AddRange(values.Where(v => HexColourPattern().IsMatch(v)).Select(v => $"{path}: hex colour in \"{v}\""));
        }

        return offenders;
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
