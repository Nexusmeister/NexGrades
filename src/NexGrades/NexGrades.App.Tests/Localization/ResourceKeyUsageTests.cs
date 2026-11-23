using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NexGrades.App.Tests.Localization;

/// <summary>
/// Checks the link between the code and the <c>*Strings.resx</c> files. Text is looked up by key at run time, so the
/// compiler cannot catch a typo in a key or a key nobody uses any more; these rules scan the App's sources instead:
/// every key a source refers to exists in the neutral resx of its area, every neutral key is referred to, and no UI
/// text is a literal in C# code. The rules are helpers over in-memory text, so negative fixtures prove each can fail.
/// </summary>
public static partial class ResourceKeyUsageTests
{
    // Keys that are built at run time and so never appear as a whole literal in a source file. Each needs a reason here.
    // None exist: every key is written out in full, which is what lets EveryKey_IsUsed find dead ones.
    private static readonly HashSet<string> _dynamicKeys = [];

    // C# literals that hold a letter and are allowed as UI text. Each needs a reason here.
    private static readonly HashSet<string> _allowedCSharpTexts =
    [
        // The brand name is the same in every language.
        "NexGrades",
    ];

    private static readonly string[] _uiTextProperties = ["Title", "Content", "PrimaryButtonText", "SecondaryButtonText", "CloseButtonText"];

    [GeneratedRegex(@"\{loc:Text\s+(?:Key\s*=\s*)?(?<key>[A-Za-z0-9_]+)\s*\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XamlKeyPattern();

    [GeneratedRegex(@"TextResources\s*\.\s*(?:Text|Format|Get)\(\s*""(?<key>[^""]*)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CallKeyPattern();

    [GeneratedRegex(@"(?<![\w=!<>])(?<name>Title|Content|PrimaryButtonText|SecondaryButtonText|CloseButtonText)\s*=(?![=>])\s*(?<literal>\$?@?""(?:[^""\\]|\\.)*"")", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AssignmentPattern();

    [GeneratedRegex(@"\.\s*Show(?:Async)?\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShowCallPattern();

    // ----- Rules over the real sources -----

    [Fact]
    public static void Sources_AreFound()
    {
        var sources = LoadSources();

        Assert.Contains(sources, s => s.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sources, s => s.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(LoadNeutralKeys());
    }

    [Fact]
    public static void EveryKey_IsUsed()
    {
        var neutral = LoadNeutralKeys();
        var references = FindReferences(LoadSources(), neutral.Keys);

        var offenders = FindUnusedKeys(neutral, references.Select(r => r.Key).ToHashSet(StringComparer.Ordinal), _dynamicKeys);

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void DynamicKeys_AreKeysOfTheNeutralResx()
    {
        var all = LoadNeutralKeys().SelectMany(a => a.Value).ToHashSet(StringComparer.Ordinal);

        Assert.True(_dynamicKeys.IsSubsetOf(all), "An allow-listed dynamic key is not in any neutral resx.");
    }

    [Fact]
    public static void EveryReferencedKey_ExistsInTheNeutralResx()
    {
        var neutral = LoadNeutralKeys();
        var references = FindReferences(LoadSources(), neutral.Keys);

        var offenders = FindMissingKeys(references, neutral);

        Assert.NotEmpty(references);
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void CSharpUiText_IsNotALiteral_InAnySourceFile()
    {
        var sources = LoadSources().Where(s => s.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();

        var offenders = FindLiteralCSharpUiText(sources, _allowedCSharpTexts);

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    // ----- Rule helpers, tested below with in-memory fixtures -----

    private sealed record Source(string Path, string Text);

    private sealed record KeyReference(string Key, string Location);

    private static List<string> FindUnusedKeys(IReadOnlyDictionary<string, HashSet<string>> neutral, HashSet<string> referenced, HashSet<string> dynamicKeys) =>
        [.. neutral
            .SelectMany(a => a.Value.Select(k => (Area: a.Key, Key: k)))
            .Where(k => !referenced.Contains(k.Key) && !dynamicKeys.Contains(k.Key))
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Area}: {k.Key} is in the resx but no source refers to it; delete it (and its German text) or use it")];

    private static List<string> FindMissingKeys(IEnumerable<KeyReference> references, Dictionary<string, HashSet<string>> neutral) =>
        [.. references
            .Where(r => !neutral.TryGetValue(AreaOfKey(r.Key), out var keys) || !keys.Contains(r.Key))
            .Select(r => $"{r.Location}: key '{r.Key}' is not in the neutral resx of its area")];

    /// <summary>
    /// Finds the keys a set of sources refer to: <c>{loc:Text Key}</c> in XAML; in C# the literal first argument of
    /// <c>TextResources.Text/Format/Get</c>, and any literal that is a whole key of a known area (so both branches of
    /// <c>cond ? "A_B" : "A_C"</c> count).
    /// </summary>
    private static List<KeyReference> FindReferences(IEnumerable<Source> sources, IEnumerable<string> areas)
    {
        var areaPattern = new Regex(
            $@"""(?<key>(?:{string.Join('|', areas.Select(Regex.Escape))})_[A-Za-z0-9_]+)""",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        var references = new List<KeyReference>();

        foreach (var (path, rawText) in sources)
        {
            if (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                references.AddRange(XamlKeyPattern().Matches(rawText).Select(m => new KeyReference(m.Groups["key"].Value, Location(path, rawText, m.Index))));
                continue;
            }

            var text = BlankComments(rawText);
            var found = new Dictionary<int, string>();
            foreach (Match match in CallKeyPattern().Matches(text))
            {
                found[match.Groups["key"].Index] = match.Groups["key"].Value;
            }

            foreach (Match match in areaPattern.Matches(text))
            {
                found[match.Groups["key"].Index] = match.Groups["key"].Value;
            }

            references.AddRange(found.Select(f => new KeyReference(f.Value, Location(path, text, f.Key))));
        }

        return references;
    }

    private static List<string> FindLiteralCSharpUiText(IEnumerable<Source> sources, HashSet<string> allowed)
    {
        var offenders = new List<string>();

        foreach (var (path, rawText) in sources)
        {
            var text = BlankComments(rawText);

            foreach (Match match in AssignmentPattern().Matches(text))
            {
                var literal = match.Groups["literal"].Value;
                if (IsOffendingLiteral(literal, allowed))
                {
                    offenders.Add($"{Location(path, text, match.Index)}: {match.Groups["name"].Value} = {literal} is a literal; use TextResources");
                }
            }

            foreach (Match match in ShowCallPattern().Matches(text))
            {
                var args = SplitArguments(text, match.Index + match.Length);
                foreach (var arg in args.Take(2))
                {
                    if (IsOffendingLiteral(arg.Trim(), allowed))
                    {
                        offenders.Add($"{Location(path, text, match.Index)}: Show call passes the literal {arg.Trim()}; use TextResources");
                    }
                }
            }
        }

        return offenders;
    }

    // A literal is a string token (plain, interpolated or verbatim) whose text holds a letter and is not allow-listed.
    private static bool IsOffendingLiteral(string argument, HashSet<string> allowed)
    {
        var start = argument.IndexOf('"', StringComparison.Ordinal);
        if (start < 0 || argument[..start].Trim('$', '@').Length != 0 || !argument.EndsWith('"'))
        {
            return false;
        }

        var inner = argument[(start + 1)..^1];
        return inner.Any(char.IsLetter) && !allowed.Contains(inner);
    }

    // Splits the arguments of a call at top-level commas, starting right after its opening parenthesis.
    private static List<string> SplitArguments(string text, int start)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                var end = SkipString(text, i);
                current.Append(text, i, end - i);
                i = end - 1;
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0)
                {
                    break;
                }

                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                args.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        args.Add(current.ToString());
        return args;
    }

    // Returns the index after the closing quote of the string that starts at the given index.
    private static int SkipString(string text, int start)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == '"')
            {
                return i + 1;
            }
        }

        return text.Length;
    }

    // Replaces // comments with spaces (keeping the line count), so a key named in a comment is not a reference. A "//" inside a
    // string, such as a URL, is left alone.
    private static string BlankComments(string text)
    {
        var builder = new StringBuilder(text.Length);
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                builder.Append(c);
                if (c == '\\' && i + 1 < text.Length)
                {
                    builder.Append(text[++i]);
                }
                else if (c is '"' or '\n')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                builder.Append(c);
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    builder.Append(' ');
                    i++;
                }

                if (i < text.Length)
                {
                    builder.Append('\n');
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    // ----- Negative fixtures -----

    [Fact]
    public static void FindUnusedKeys_Flags_KeyNoSourceRefersTo()
    {
        var neutral = Neutral(("X", ["X_Used", "X_Dead"]));

        var offenders = FindUnusedKeys(neutral, ["X_Used"], []);

        Assert.Single(offenders);
        Assert.Contains("X_Dead", offenders[0], StringComparison.Ordinal);
    }

    [Fact]
    public static void FindUnusedKeys_Accepts_UsedAndAllowListedDynamicKeys() =>
        Assert.Empty(FindUnusedKeys(Neutral(("X", ["X_Used", "X_Built"])), ["X_Used"], ["X_Built"]));

    [Fact]
    public static void FindMissingKeys_Flags_TypoAndUnknownAreaButAcceptsExistingKey()
    {
        var neutral = Neutral(("X", ["X_Save"]));
        KeyReference[] references = [new("X_Save", "a.cs:1"), new("X_Sav", "b.cs:2"), new("Y_Save", "c.xaml:3")];

        var offenders = FindMissingKeys(references, neutral);

        Assert.Equal(["b.cs:2: key 'X_Sav' is not in the neutral resx of its area", "c.xaml:3: key 'Y_Save' is not in the neutral resx of its area"], offenders);
    }

    [Fact]
    public static void FindReferences_ReadsXamlMarkupExtensionsInBothForms()
    {
        var sources = new[] { new Source("a.xaml", """<Button Content="{loc:Text X_A}" ToolTip="{loc:Text Key=X_B}" Title="{Binding T, StringFormat={loc:Text X_C}}"/>""") };

        var keys = FindReferences(sources, ["X"]).Select(r => r.Key);

        Assert.Equal(["X_A", "X_B", "X_C"], keys);
    }

    [Fact]
    public static void FindReferences_ReadsCSharpCallsAndBothBranchesOfATernary()
    {
        var sources = new[]
        {
            new Source("a.cs", """
                var a = TextResources.Text("X_A");
                var b = TextResources.Format(flag ? "X_B" : "X_C", 1);
                var c = TextResources.Get("Typo_Key", culture);
                """),
        };

        var keys = FindReferences(sources, ["X"]).Select(r => r.Key).Order(StringComparer.Ordinal);

        Assert.Equal(["Typo_Key", "X_A", "X_B", "X_C"], keys);
    }

    [Fact]
    public static void FindReferences_IgnoresKeysNamedInComments()
    {
        var sources = new[] { new Source("a.cs", "// see \"X_Old\"\n/// <c>\"X_Doc\"</c>\nvar a = \"https://x/X_Url\"; // \"X_Tail\"\nvar b = TextResources.Text(\"X_Real\");") };

        var keys = FindReferences(sources, ["X"]).Select(r => r.Key);

        Assert.Equal(["X_Real"], keys);
    }

    [Fact]
    public static void FindLiteralCSharpUiText_Flags_LiteralSnackbarTitleMessageAndAssignments()
    {
        var sources = new[]
        {
            new Source("a.cs", """
                snackbar.Show("Saved", TextResources.Text("X_A"), appearance, icon, time);
                snackbar.Show(TextResources.Text("X_B"), "It worked", appearance, icon, time);
                dialog.Title = "Hello";
                var d = new ContentDialog { PrimaryButtonText = "OK", CloseButtonText = $"Close {x}" };
                """),
        };

        var offenders = FindLiteralCSharpUiText(sources, []);

        Assert.Equal(5, offenders.Count);
    }

    [Fact]
    public static void FindLiteralCSharpUiText_Allows_ResourceTextNonLetterLiteralsAllowListAndOtherCalls()
    {
        var sources = new[]
        {
            new Source("a.cs", """
                snackbar.Show(TextResources.Text("X_A"), message, appearance, icon, TimeSpan.FromSeconds(5));
                snackbar.Show(TextResources.Text("X_A"), TextResources.Format("X_B", "a, b"), appearance, icon, time);
                window.Title = "NexGrades";
                row.Content = "-";
                if (a.Title == "Hello") { }
                """),
        };

        var offenders = FindLiteralCSharpUiText(sources, ["NexGrades"]);

        Assert.Empty(offenders);
    }

    // ----- Plumbing -----

    private static Dictionary<string, HashSet<string>> Neutral(params (string Area, string[] Keys)[] areas) =>
        areas.ToDictionary(a => a.Area, a => a.Keys.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string AreaOfKey(string key)
    {
        var separator = key.IndexOf('_', StringComparison.Ordinal);
        return separator < 0 ? key : key[..separator];
    }

    private static string Location(string path, string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return $"{path}:{line}";
    }

    private static DirectoryInfo FindAppDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NexGrades.App")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir;
    }

    private static IEnumerable<string> EnumerateFiles(DirectoryInfo root, string pattern)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(root.FullName, "NexGrades.App"), pattern, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
                && !p.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase));
    }

    private static List<Source> LoadSources()
    {
        var root = FindAppDirectory();
        return [.. EnumerateFiles(root, "*.cs").Concat(EnumerateFiles(root, "*.xaml"))
            .Select(p => new Source(Path.GetRelativePath(root.FullName, p), File.ReadAllText(p)))];
    }

    // Maps each area (the file name before "Strings.resx") to the keys of its neutral resx.
    private static Dictionary<string, HashSet<string>> LoadNeutralKeys()
    {
        var root = FindAppDirectory();
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var path in EnumerateFiles(root, "*Strings.resx"))
        {
            var name = Path.GetFileName(path);
            var area = name[..^"Strings.resx".Length];
            var doc = XDocument.Load(path);
            result[area] = [.. doc.Root!.Elements("data")
                .Where(d => d.Attribute("type") is null && d.Attribute("mimetype") is null)
                .Select(d => (string)d.Attribute("name")!)];
        }

        return result;
    }
}
