using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NexGrades.App.Localization;

namespace NexGrades.App.Tests.Localization;

/// <summary>
/// Checks the <c>*Strings.resx</c> files: German is complete, differs from English, keeps the placeholders, and every
/// area is registered in <see cref="TextResources"/>. The rules are helpers over in-memory entries, so negative
/// fixtures prove each of them can fail.
/// </summary>
public static partial class ResourceFileTests
{
    private const string FormatSuffix = "_Format";

    // Keys whose German text is the same as the English one on purpose: the names of the languages, "System" and a few loan words (Id, Name, Optional) are
    // the same in both languages.
    private static readonly HashSet<string> _sameInGerman =
    [
        "Settings_Language_System",
        "Settings_Language_English",
        "Settings_Language_German",
        "Settings_Updates_Header",
        "Classes_Grid_Name_Header",
        "Subjects_Overview_Id_Header",
        "Subjects_Overview_Name_Header",
        "Students_Overview_Id_Header",
        "Students_StudentPage_Comment_Placeholder",
    ];

    private static readonly CultureInfo _german = CultureInfo.GetCultureInfo("de");

    [GeneratedRegex(@"\{(?<index>\d+)(?:[,:][^{}]*)?\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"^(?<area>.+)Strings(?<german>\.de)?\.resx$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FileNamePattern();

    // ----- Rules over the real files -----

    [Fact]
    public static void EveryNeutralResx_HasGermanFile()
    {
        var files = LoadFiles();

        var offenders = FindMissingGermanFiles(files.Neutral.Keys, files.German.Keys);

        Assert.NotEmpty(files.Neutral);
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void GermanResx_HasExactlyTheNeutralKeys()
    {
        var offenders = PerArea(FindKeyDifferences);

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void GermanValues_AreNotEmpty()
    {
        var files = LoadFiles();

        var offenders = files.German.SelectMany(f => FindEmptyValues(f.Key, f.Value)).ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void GermanValues_DifferFromEnglish_UnlessAllowListed()
    {
        var offenders = PerArea((area, neutral, german) => FindUnchangedValues(area, neutral, german, _sameInGerman));

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void Placeholders_MatchBetweenLanguages()
    {
        var offenders = PerArea(FindPlaceholderMismatches);

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void AllValues_ParseAsCompositeFormat_InBothLanguages()
    {
        var files = LoadFiles();

        var offenders = files.Neutral.Concat(files.German).SelectMany(f => FindUnparseableValues(f.Key, f.Value)).ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void FormatSuffix_MatchesPlaceholders()
    {
        var files = LoadFiles();

        var offenders = files.Neutral.Concat(files.German).SelectMany(f => FindFormatSuffixOffenders(f.Key, f.Value)).ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void FormatKeys_HaveTranslatorComment()
    {
        var files = LoadFiles();

        var offenders = files.Neutral.SelectMany(f => FindFormatKeysWithoutComment(f.Key, f.Value)).ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void EveryResxArea_IsRegistered()
    {
        var files = LoadFiles();

        var offenders = FindAreaRegistrationOffenders([.. files.Neutral.Keys.Select(AreaOf)], [.. TextResources.Areas]);

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void EveryKey_StartsWithItsAreaPrefix()
    {
        var files = LoadFiles();

        var offenders = files.Neutral.Concat(files.German).SelectMany(f => FindPrefixOffenders(f.Key, f.Value)).ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public static void GermanSatellite_HasTheNeutralKeys_ForEveryRegisteredArea()
    {
        Assert.NotEmpty(TextResources.Areas);

        foreach (var area in TextResources.Areas)
        {
            var manager = TextResources.GetManager(area);
            var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);
            var german = manager.GetResourceSet(_german, createIfNotExists: true, tryParents: false);

            Assert.True(neutral is not null, $"{area}: the neutral resource is not embedded.");
            Assert.True(german is not null, $"{area}: the German satellite assembly is missing.");
            Assert.Equal(KeysOf(neutral), KeysOf(german));
        }
    }

    // ----- Rule helpers, tested below with in-memory fixtures -----

    private static List<string> FindMissingGermanFiles(IEnumerable<string> neutralPaths, IEnumerable<string> germanPaths)
    {
        var german = germanPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. neutralPaths
            .Where(p => !german.Contains(GermanPathOf(p)))
            .Select(p => $"{p}: no German file {GermanPathOf(p)}")];
    }

    private static List<string> FindKeyDifferences(string area, IReadOnlyDictionary<string, ResxEntry> neutral, IReadOnlyDictionary<string, ResxEntry> german) =>
    [
        .. neutral.Keys.Where(k => !german.ContainsKey(k)).Select(k => $"{area}: German is missing key {k}"),
        .. german.Keys.Where(k => !neutral.ContainsKey(k)).Select(k => $"{area}: German has extra key {k}"),
    ];

    private static List<string> FindEmptyValues(string path, IReadOnlyDictionary<string, ResxEntry> entries) =>
        [.. entries.Where(e => string.IsNullOrWhiteSpace(e.Value.Value)).Select(e => $"{path}: {e.Key} is empty")];

    private static List<string> FindUnchangedValues(string area, IReadOnlyDictionary<string, ResxEntry> neutral, IReadOnlyDictionary<string, ResxEntry> german, HashSet<string> allowed) =>
    [
        .. neutral
            .Where(e => german.TryGetValue(e.Key, out var other)
                && string.Equals(e.Value.Value, other.Value, StringComparison.Ordinal)
                && !allowed.Contains(e.Key))
            .Select(e => $"{area}: {e.Key} is the same in German; translate it or add it to the allow-list"),
    ];

    private static List<string> FindPlaceholderMismatches(string area, IReadOnlyDictionary<string, ResxEntry> neutral, IReadOnlyDictionary<string, ResxEntry> german) =>
    [
        .. neutral
            .Where(e => german.TryGetValue(e.Key, out var other)
                && !PlaceholdersOf(e.Value.Value).SetEquals(PlaceholdersOf(other.Value)))
            .Select(e => $"{area}: {e.Key} has different placeholders in German"),
    ];

    private static List<string> FindUnparseableValues(string path, IReadOnlyDictionary<string, ResxEntry> entries)
    {
        var offenders = new List<string>();
        foreach (var (key, entry) in entries)
        {
            try
            {
                _ = CompositeFormat.Parse(entry.Value);
            }
            catch (FormatException)
            {
                offenders.Add($"{path}: {key} is not a valid format string");
            }
        }

        return offenders;
    }

    private static List<string> FindFormatSuffixOffenders(string path, IReadOnlyDictionary<string, ResxEntry> entries) =>
        [.. entries
            .Where(e => PlaceholdersOf(e.Value.Value).Count > 0 != e.Key.EndsWith(FormatSuffix, StringComparison.Ordinal))
            .Select(e => $"{path}: {e.Key} must end in {FormatSuffix} exactly when its text has placeholders")];

    private static List<string> FindFormatKeysWithoutComment(string path, IReadOnlyDictionary<string, ResxEntry> entries) =>
        [.. entries
            .Where(e => e.Key.EndsWith(FormatSuffix, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(e.Value.Comment))
            .Select(e => $"{path}: {e.Key} has no <comment> describing its placeholders")];

    private static List<string> FindAreaRegistrationOffenders(IReadOnlyCollection<string> fileAreas, IReadOnlyCollection<string> registeredAreas) =>
    [
        .. fileAreas.Except(registeredAreas, StringComparer.Ordinal).Select(a => $"{a}: has a resx file but is not registered in TextResources"),
        .. registeredAreas.Except(fileAreas, StringComparer.Ordinal).Select(a => $"{a}: is registered in TextResources but has no resx file"),
    ];

    private static List<string> FindPrefixOffenders(string path, IReadOnlyDictionary<string, ResxEntry> entries)
    {
        var prefix = AreaOf(path) + "_";
        return [.. entries.Keys.Where(k => !k.StartsWith(prefix, StringComparison.Ordinal)).Select(k => $"{path}: {k} does not start with {prefix}")];
    }

    // ----- Negative fixtures -----

    [Fact]
    public static void FindMissingGermanFiles_Flags_NeutralFileWithoutGermanFile()
    {
        var offenders = FindMissingGermanFiles(["XStrings.resx", "YStrings.resx"], ["XStrings.de.resx"]);

        Assert.Equal(["YStrings.resx: no German file YStrings.de.resx"], offenders);
    }

    [Fact]
    public static void FindMissingGermanFiles_Accepts_CompletePair() =>
        Assert.Empty(FindMissingGermanFiles(["XStrings.resx"], ["XStrings.de.resx"]));

    [Fact]
    public static void FindKeyDifferences_Flags_MissingAndExtraKeys()
    {
        var offenders = FindKeyDifferences(
            "X",
            Entries(("X_A", "a"), ("X_B", "b")),
            Entries(("X_A", "a"), ("X_C", "c")));

        Assert.Equal(["X: German is missing key X_B", "X: German has extra key X_C"], offenders);
    }

    [Fact]
    public static void FindKeyDifferences_Accepts_SameKeys() =>
        Assert.Empty(FindKeyDifferences("X", Entries(("X_A", "a")), Entries(("X_A", "b"))));

    [Fact]
    public static void FindEmptyValues_Flags_EmptyAndWhitespaceValues()
    {
        var offenders = FindEmptyValues("f", Entries(("X_A", ""), ("X_B", "  "), ("X_C", "ok")));

        Assert.Equal(2, offenders.Count);
    }

    [Fact]
    public static void FindUnchangedValues_Flags_IdenticalTextNotAllowListed()
    {
        var offenders = FindUnchangedValues("X", Entries(("X_A", "Same"), ("X_B", "One")), Entries(("X_A", "Same"), ("X_B", "Eins")), []);

        Assert.Equal(["X: X_A is the same in German; translate it or add it to the allow-list"], offenders);
    }

    [Fact]
    public static void FindUnchangedValues_Accepts_IdenticalTextOnAllowList() =>
        Assert.Empty(FindUnchangedValues("X", Entries(("X_A", "Same")), Entries(("X_A", "Same")), ["X_A"]));

    [Fact]
    public static void FindPlaceholderMismatches_Flags_DifferentIndexSets()
    {
        var offenders = FindPlaceholderMismatches("X", Entries(("X_A_Format", "{0} of {1}")), Entries(("X_A_Format", "{0} von")));

        Assert.Single(offenders);
    }

    [Fact]
    public static void FindPlaceholderMismatches_Accepts_ReorderedPlaceholders() =>
        Assert.Empty(FindPlaceholderMismatches("X", Entries(("X_A_Format", "{0} of {1}")), Entries(("X_A_Format", "{1}: {0:N1}"))));

    [Fact]
    public static void FindUnparseableValues_Flags_UnbalancedBrace()
    {
        var offenders = FindUnparseableValues("f", Entries(("X_A", "Open { brace"), ("X_B", "Fine {0}"), ("X_C", "Escaped {{ ok }}")));

        Assert.Equal(["f: X_A is not a valid format string"], offenders);
    }

    [Fact]
    public static void FindFormatSuffixOffenders_Flags_PlaceholderWithoutSuffixAndSuffixWithoutPlaceholder()
    {
        var offenders = FindFormatSuffixOffenders("f", Entries(("X_A", "Has {0}"), ("X_B_Format", "Has none"), ("X_C_Format", "Has {0}"), ("X_D", "Plain")));

        Assert.Equal(2, offenders.Count);
        Assert.Contains(offenders, o => o.Contains("X_A", StringComparison.Ordinal));
        Assert.Contains(offenders, o => o.Contains("X_B_Format", StringComparison.Ordinal));
    }

    [Fact]
    public static void FindFormatSuffixOffenders_IgnoresEscapedBraces() =>
        Assert.Empty(FindFormatSuffixOffenders("f", Entries(("X_A", "Literal {{0}}"))));

    [Fact]
    public static void FindFormatKeysWithoutComment_Flags_FormatKeyWithoutComment()
    {
        var entries = new Dictionary<string, ResxEntry>
        {
            ["X_A_Format"] = new("{0}", null),
            ["X_B_Format"] = new("{0}", "0 is the count."),
            ["X_C"] = new("Plain", null),
        };

        var offenders = FindFormatKeysWithoutComment("f", entries);

        Assert.Equal(["f: X_A_Format has no <comment> describing its placeholders"], offenders);
    }

    [Fact]
    public static void FindAreaRegistrationOffenders_Flags_UnregisteredAndOrphanedAreas()
    {
        var offenders = FindAreaRegistrationOffenders(["Common", "New"], ["Common", "Old"]);

        Assert.Equal(["New: has a resx file but is not registered in TextResources", "Old: is registered in TextResources but has no resx file"], offenders);
    }

    [Fact]
    public static void FindPrefixOffenders_Flags_KeyOfAnotherArea()
    {
        var offenders = FindPrefixOffenders("SettingsStrings.resx", Entries(("Settings_A", "a"), ("Common_B", "b")));

        Assert.Equal(["SettingsStrings.resx: Common_B does not start with Settings_"], offenders);
    }

    [Fact]
    public static void ParseEntries_ReadsValueAndComment()
    {
        var doc = XDocument.Parse("""
            <root>
              <data name="X_A_Format" xml:space="preserve"><value>{0} items</value><comment>0 is the count.</comment></data>
              <data name="X_B" xml:space="preserve"><value>Plain</value></data>
            </root>
            """);

        var entries = ParseEntries(doc);

        Assert.Equal("{0} items", entries["X_A_Format"].Value);
        Assert.Equal("0 is the count.", entries["X_A_Format"].Comment);
        Assert.Null(entries["X_B"].Comment);
    }

    // ----- Plumbing -----

    private sealed record ResxEntry(string Value, string? Comment);

    private sealed record ResxFiles(
        Dictionary<string, Dictionary<string, ResxEntry>> Neutral,
        Dictionary<string, Dictionary<string, ResxEntry>> German);

    private static Dictionary<string, ResxEntry> Entries(params (string Key, string Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => new ResxEntry(e.Value, null), StringComparer.Ordinal);

    private static HashSet<int> PlaceholdersOf(string text)
    {
        // Escaped braces ({{ and }}) are literal text, not placeholders.
        var stripped = text.Replace("{{", string.Empty, StringComparison.Ordinal).Replace("}}", string.Empty, StringComparison.Ordinal);
        return [.. PlaceholderPattern().Matches(stripped).Select(m => int.Parse(m.Groups["index"].Value, CultureInfo.InvariantCulture))];
    }

    private static string AreaOf(string path) => FileNamePattern().Match(Path.GetFileName(path)).Groups["area"].Value;

    private static string GermanPathOf(string neutralPath) =>
        Path.Combine(Path.GetDirectoryName(neutralPath) ?? string.Empty, Path.GetFileNameWithoutExtension(neutralPath) + ".de.resx");

    private static List<string> PerArea(Func<string, IReadOnlyDictionary<string, ResxEntry>, IReadOnlyDictionary<string, ResxEntry>, List<string>> rule)
    {
        var files = LoadFiles();
        var offenders = new List<string>();
        foreach (var (path, neutral) in files.Neutral)
        {
            if (files.German.TryGetValue(GermanPathOf(path), out var german))
            {
                offenders.AddRange(rule(AreaOf(path), neutral, german));
            }
        }

        Assert.NotEmpty(files.Neutral);
        return offenders;
    }

    private static SortedSet<string> KeysOf(System.Resources.ResourceSet? set)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        if (set is null)
        {
            return keys;
        }

        foreach (var entry in set.Cast<System.Collections.DictionaryEntry>())
        {
            _ = keys.Add((string)entry.Key);
        }

        return keys;
    }

    private static Dictionary<string, ResxEntry> ParseEntries(XDocument doc) =>
        doc.Root!.Elements("data")
            .Where(d => d.Attribute("type") is null && d.Attribute("mimetype") is null)
            .ToDictionary(
                d => (string)d.Attribute("name")!,
                d => new ResxEntry(
                    (string?)d.Element("value") ?? string.Empty,
                    (string?)d.Element("comment")),
                StringComparer.Ordinal);

    private static ResxFiles LoadFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NexGrades.App")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var neutral = new Dictionary<string, Dictionary<string, ResxEntry>>(StringComparer.OrdinalIgnoreCase);
        var german = new Dictionary<string, Dictionary<string, ResxEntry>>(StringComparer.OrdinalIgnoreCase);
        var separator = Path.DirectorySeparatorChar;
        var paths = Directory.EnumerateFiles(Path.Combine(dir.FullName, "NexGrades.App"), "*Strings*.resx", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase)
                && !p.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase));

        foreach (var path in paths)
        {
            var match = FileNamePattern().Match(Path.GetFileName(path));
            if (!match.Success)
            {
                continue;
            }

            var relative = Path.GetRelativePath(dir.FullName, path);
            (match.Groups["german"].Success ? german : neutral)[relative] = ParseEntries(XDocument.Load(path));
        }

        return new ResxFiles(neutral, german);
    }
}
