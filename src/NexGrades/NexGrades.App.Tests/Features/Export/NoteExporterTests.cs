using System.IO;
using NexGrades.App.Features.Export;
using NexGrades.Domain.Export;

namespace NexGrades.App.Tests.Features.Export;

public class NoteExporterTests
{
    private static NoteExportTable TableOf(string first, string last, DateOnly from, DateOnly to) =>
        new(first, last, from, to, false, new DateTime(2026, 3, 1), []);

    [Fact]
    public void SuggestFileName_UsesNamePlusIsoRangePattern()
    {
        var table = TableOf("Test", "Alpha", new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31));

        Assert.Equal("Notes Alpha Test 2025-08-01–2026-07-31.xlsx", NoteExporter.SuggestFileName(table));
    }

    [Fact]
    public void SuggestFileName_ReplacesInvalidFileNameCharacters()
    {
        var table = TableOf("Te/st", "Al:pha?", new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31));

        var name = NoteExporter.SuggestFileName(table);

        Assert.Equal("Notes Al-pha- Te-st 2025-08-01–2026-07-31.xlsx", name);
        Assert.DoesNotContain(name, c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0);
    }

    [Fact]
    public void SuggestFileName_ThrowsArgumentNull_ForNullTable() =>
        Assert.Throws<ArgumentNullException>(() => NoteExporter.SuggestFileName(null!));
}
