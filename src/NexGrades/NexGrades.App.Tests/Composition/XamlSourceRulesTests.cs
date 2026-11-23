using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Composition;

/// <summary>
/// Scans the App's XAML sources for mistakes that only show at runtime. WPF-UI replaces the style of a
/// <c>ui:TextBlock</c>, so a <c>Visibility</c> setter or trigger in its own style never applies; bind
/// <c>Visibility</c> directly instead. Not covered: styles applied by type (an implicit style or a keyed style
/// reached only through <c>TargetType</c>), and styles set from code or through a <c>BasedOn</c> chain.
/// </summary>
public partial class XamlSourceRulesTests
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
        var checkedTitles = 0;

        foreach (var (path, doc) in LoadXamlFiles())
        {
            var root = doc.Root!;
            var className = (string?)root.Attribute(_xaml + "Class");
            var title = ResolveTitle((string?)root.Attribute("Title"));
            if (className is null || title is null)
            {
                continue;
            }

            checkedTitles++;
            var shortName = className[(className.LastIndexOf('.') + 1)..];
            if (string.Equals(title, shortName, StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{path}: Title '{title}' equals the class name");
            }
            else if (IsIdentifierLike(title) && !_allowedLiteralTexts.Contains(title))
            {
                offenders.Add($"{path}: Title '{title}' looks like an identifier");
            }
        }

        Assert.True(checkedTitles > 0, "No page title was found, so the rule would pass vacuously.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void ResolveTitle_ReadsTheNeutralEnglishText_OfALocalizedTitle() =>
        Assert.Equal(TextResources.Get("Common_Save", CultureInfo.InvariantCulture), ResolveTitle("{loc:Text Common_Save}"));

    [Fact]
    public void ResolveTitle_ReturnsALiteralTitleAsItIs() =>
        Assert.Equal("Students", ResolveTitle("Students"));

    [Theory]
    [InlineData("ClassPage")]
    [InlineData("Class_Page")]
    [InlineData("studentViewModel")]
    [InlineData("Settings_Language_Header")]
    public void IsIdentifierLike_Flags_IdentifierShapedText(string text) =>
        Assert.True(IsIdentifierLike(text));

    [Theory]
    [InlineData("Students")]
    [InlineData("Add class")]
    [InlineData("School year")]
    [InlineData("Klassen")]
    [InlineData("")]
    public void IsIdentifierLike_Accepts_PlainText(string text) =>
        Assert.False(IsIdentifierLike(text));

    private static string? ResolveTitle(string? title)
    {
        if (title is null)
        {
            return null;
        }

        var match = LocTextPattern().Match(title);
        return match.Success ? TextResources.Get(match.Groups["key"].Value, CultureInfo.InvariantCulture) : title;
    }

    private static bool IsIdentifierLike(string text) =>
        text.Length > 0 && IdentifierPattern().IsMatch(text);

    [GeneratedRegex(@"^\{loc:Text\s+(?:Key=)?(?<key>\w+)\s*\}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LocTextPattern();

    // One word without spaces that is PascalCase/camelCase with at least two humps, or contains an underscore.
    [GeneratedRegex(@"^(?:[A-Za-z0-9]+_[A-Za-z0-9_]*|[a-z][a-z0-9]*(?:[A-Z][a-z0-9]*)+|[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdentifierPattern();

    // ----- UI text is never a literal in XAML -----

    private static readonly HashSet<string> _textAttributes =
    [
        "Content", "Header", "Text", "ToolTip", "ToolTipService.ToolTip", "PlaceholderText", "Title", "Message",
        "PrimaryButtonText", "SecondaryButtonText", "CloseButtonText", "AutomationProperties.Name", "AutomationProperties.HelpText",
    ];

    // Literal values that are allowed: the brand name is not translated.
    private static readonly HashSet<string> _allowedLiteralTexts =
    [
        "NexGrades",
    ];

    [GeneratedRegex(@"StringFormat\s*=\s*(?:'(?<value>[^']*)'|(?<value>[^,}]*))", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StringFormatPattern();

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BracesPattern();

    [Fact]
    public void XamlUiText_IsNotALiteral_InAnyXamlFile()
    {
        var files = LoadXamlFiles();

        var offenders = FindLiteralUiTextOffenders(files);

        Assert.True(files.SelectMany(f => f.Document.Descendants().Attributes()).Any(a => (string?)a is { } v && v.StartsWith("{loc:Text", StringComparison.Ordinal)),
            "No {loc:Text} was found, so the rule would pass vacuously.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void FindLiteralUiTextOffenders_Flags_LiteralTextInUiAttributes()
    {
        var doc = Parse("""
            <Grid>
                <ui:Button Content="Save" ToolTip="Saves it" AutomationProperties.Name="Save button"/>
                <ui:TextBox PlaceholderText="Name" ToolTipService.ToolTip="Hint"/>
                <ui:TextBlock Text="{Binding X, StringFormat='Average: {0:N1}'}"/>
            </Grid>
            """);

        var offenders = FindLiteralUiTextOffenders([("bad.xaml", doc)]);

        Assert.Equal(6, offenders.Count);
        Assert.Contains(offenders, o => o.Contains("Content=\"Save\"", StringComparison.Ordinal));
        Assert.Contains(offenders, o => o.Contains("StringFormat", StringComparison.Ordinal));
    }

    [Fact]
    public void FindLiteralUiTextOffenders_Allows_BindingsLocalizedTextDesignTimeValuesSymbolsAndTheBrand()
    {
        var doc = Parse("""
            <Grid xmlns:d="http://schemas.microsoft.com/expression/blend/2008">
                <ui:Button Content="{loc:Text Common_Save}" ToolTip="{Binding Tip}" d:Content="Design time text"/>
                <ui:TextBlock Text="{Binding X, StringFormat={loc:Text Common_Format}}"/>
                <ui:TextBlock Text="{Binding X, StringFormat='{}{0:N1}'}"/>
                <ui:TextBlock Text="+" Title="NexGrades"/>
                <ui:TextBlock Text="12 / 34" Name="NotUiText" Tag="Plain tag"/>
            </Grid>
            """);

        Assert.Empty(FindLiteralUiTextOffenders([("ok.xaml", doc)]));
    }

    private static List<string> FindLiteralUiTextOffenders(IReadOnlyList<(string Path, XDocument Document)> files)
    {
        var offenders = new List<string>();

        foreach (var (path, doc) in files)
        {
            foreach (var attribute in doc.Descendants().Attributes())
            {
                var value = attribute.Value;
                if (attribute.Name.Namespace != XNamespace.None || value.StartsWith('{') && !value.StartsWith("{}", StringComparison.Ordinal))
                {
                    // Design-time (d:) attributes and markup extensions are not literals; a binding's StringFormat is checked below.
                    if (attribute.Name.Namespace == XNamespace.None && _textAttributes.Contains(attribute.Name.LocalName))
                    {
                        AddStringFormatOffenders(offenders, path, attribute, value);
                    }

                    continue;
                }

                if (!_textAttributes.Contains(attribute.Name.LocalName) || _allowedLiteralTexts.Contains(value))
                {
                    continue;
                }

                if (HasLetter(value))
                {
                    offenders.Add($"{path}: {attribute.Name.LocalName}=\"{value}\" is a literal; use {{loc:Text Key}}");
                }
            }
        }

        return offenders;
    }

    private static void AddStringFormatOffenders(List<string> offenders, string path, XAttribute attribute, string value)
    {
        foreach (Match match in StringFormatPattern().Matches(value))
        {
            var format = match.Groups["value"].Value.Trim();
            if (format.StartsWith('{') && !format.StartsWith("{}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = format.StartsWith("{}", StringComparison.Ordinal) ? format[2..] : format;
            if (HasLetter(BracesPattern().Replace(text, string.Empty)))
            {
                offenders.Add($"{path}: {attribute.Name.LocalName} StringFormat '{format}' holds literal text; use a localized format string");
            }
        }
    }

    private static bool HasLetter(string text) => text.Any(char.IsLetter);

    [Fact]
    public void TextBoxes_UpdateOnEveryKeystroke_InFilesWithDefaultOrCancelButtons()
    {
        var offenders = FindLostFocusTextBoxOffenders(LoadXamlFiles());

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void FindLostFocusTextBoxOffenders_Flags_TextBindingWithoutPropertyChanged()
    {
        var doc = Parse("""
            <Grid>
                <ui:Button IsDefault="True"/>
                <ui:TextBox Text="{Binding ViewModel.Name}"/>
            </Grid>
            """);

        Assert.Single(FindLostFocusTextBoxOffenders([("bad.xaml", doc)]));
    }

    [Fact]
    public void FindLostFocusTextBoxOffenders_Allows_PropertyChangedBindingAndFilesWithoutDefaultButton()
    {
        var withTrigger = Parse("""
            <Grid>
                <ui:Button IsCancel="True"/>
                <ui:TextBox Text="{Binding ViewModel.Name, UpdateSourceTrigger=PropertyChanged}"/>
            </Grid>
            """);
        var withoutButton = Parse("""<ui:TextBox Text="{Binding ViewModel.Name}"/>""");

        Assert.Empty(FindLostFocusTextBoxOffenders([("ok.xaml", withTrigger), ("ok2.xaml", withoutButton)]));
    }

    /// <summary>
    /// Enter and Escape fire <c>IsDefault</c> and <c>IsCancel</c> buttons before a text box loses focus, so a
    /// default-trigger text binding hands the command a stale value; such files must bind on every keystroke.
    /// </summary>
    private static List<string> FindLostFocusTextBoxOffenders(IReadOnlyList<(string Path, XDocument Document)> files)
    {
        var offenders = new List<string>();

        foreach (var (path, doc) in files)
        {
            var hasDefaultOrCancel = doc.Descendants().Any(e =>
                e.Name.LocalName == "Button"
                && ((string?)e.Attribute("IsDefault") == "True" || (string?)e.Attribute("IsCancel") == "True"));
            if (!hasDefaultOrCancel)
            {
                continue;
            }

            foreach (var box in doc.Descendants().Where(e => e.Name.LocalName == "TextBox"))
            {
                var text = (string?)box.Attribute("Text");
                if (text is not null
                    && text.Contains("{Binding", StringComparison.Ordinal)
                    && !text.Contains("UpdateSourceTrigger=PropertyChanged", StringComparison.Ordinal))
                {
                    offenders.Add($"{path}: TextBox Text binding '{text}' lacks UpdateSourceTrigger=PropertyChanged");
                }
            }
        }

        return offenders;
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
