using MindAttic.Export.Paths;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Paths;

[TestFixture]
public class ExportPathsTests
{
    private static readonly char[] WindowsInvalid = "\\/:*?\"<>|".ToCharArray();

    public static IEnumerable<TestCaseData> InvalidChars()
    {
        var all = Path.GetInvalidFileNameChars().Concat(WindowsInvalid).Concat(['\'', '’']).Distinct().OrderBy(c => c);
        foreach (var c in all)
            yield return new TestCaseData(c).SetName($"SanitizeTitle_removes_U+{(int)c:X4}");
    }

    [TestCaseSource(nameof(InvalidChars))]
    public void SanitizeTitle_removes_invalid_char(char c)
    {
        Assert.That(ExportPaths.SanitizeTitle($"Ab{c}cd"), Is.EqualTo("Abcd"));
        Assert.That(ExportPaths.SanitizeTitle($"{c}{c}Title{c}"), Is.EqualTo("Title"));
        Assert.That(ExportPaths.SanitizeTitle($"{c}"), Is.EqualTo("untitled"));
    }

    public static IEnumerable<TestCaseData> ControlChars() =>
        Enumerable.Range(0, 0x20).Concat(Enumerable.Range(0x7F, 0x21)).Select(i => new TestCaseData((char)i).SetName($"SanitizeTitle_removes_control_U+{i:X4}"));

    [TestCaseSource(nameof(ControlChars))]
    public void SanitizeTitle_removes_control(char c)
    {
        var result = ExportPaths.SanitizeTitle($"Line{c}Bell");
        // Whitespace controls (\t \n \v \f \r, U+0085) are removed too: char.IsControl is checked
        // before whitespace collapsing, so nothing becomes a space.
        Assert.That(result, Is.EqualTo("LineBell"));
    }

    public static IEnumerable<TestCaseData> ReservedNames()
    {
        string[] bases = ["CON", "PRN", "AUX", "NUL"];
        var stems = bases.ToList();
        foreach (var prefix in new[] { "COM", "LPT" })
        {
            for (var d = 0; d <= 9; d++) stems.Add($"{prefix}{d}");
            stems.Add($"{prefix}¹"); stems.Add($"{prefix}²"); stems.Add($"{prefix}³");
        }
        foreach (var stem in stems)
            foreach (var variant in new[] { stem, stem.ToLowerInvariant(), char.ToUpperInvariant(stem[0]) + stem[1..].ToLowerInvariant() })
                foreach (var ext in new[] { "", ".txt", ".tar.gz", "." , " .docx" })
                    yield return new TestCaseData(variant + ext).SetName($"IsReservedDeviceName_true({variant}{ext.Replace(" ", "␠")})");
    }

    [TestCaseSource(nameof(ReservedNames))]
    public void IsReservedDeviceName_true(string name) =>
        Assert.That(ExportPaths.IsReservedDeviceName(name), Is.True);

    [TestCase("CONSOLE")]
    [TestCase("CO")]
    [TestCase("COM")]
    [TestCase("COM10")]
    [TestCase("COMA")]
    [TestCase("COM⁴")]
    [TestCase("LPT")]
    [TestCase("LPT10")]
    [TestCase("LPTX")]
    [TestCase("NULL")]
    [TestCase("AUXILIARY")]
    [TestCase("PRNT")]
    [TestCase("xCON")]
    [TestCase(" CON")]
    [TestCase("CON_")]
    [TestCase("my.CON")]
    [TestCase("")]
    [TestCase("Bushido Coda")]
    [TestCase("COM١")]
    public void IsReservedDeviceName_false(string name) =>
        Assert.That(ExportPaths.IsReservedDeviceName(name), Is.False);

    public static IEnumerable<TestCaseData> ReservedTitles() =>
        ReservedNames().Select(tc => (string)tc.Arguments[0]!)
            .Where(n => !n.Contains('.') )
            .Select(n => new TestCaseData(n).SetName($"SanitizeTitle_prefixes_reserved({n})"));

    [TestCaseSource(nameof(ReservedTitles))]
    public void SanitizeTitle_prefixes_reserved(string name) =>
        Assert.That(ExportPaths.SanitizeTitle(name), Is.EqualTo("_" + name));

    [TestCase("a  b", "a b")]
    [TestCase("a  b", "a b")]
    [TestCase("  padded  ", "padded")]
    [TestCase("tab sep", "tab sep")]
    [TestCase("a 　 b", "a b")]
    [TestCase("many     spaces   here", "many spaces here")]
    [TestCase("x . . .", "x")]
    [TestCase("End.", "End")]
    [TestCase("End. ", "End")]
    [TestCase("End .", "End")]
    [TestCase("...", "untitled")]
    [TestCase("   ", "untitled")]
    [TestCase("", "untitled")]
    [TestCase("?*?", "untitled")]
    [TestCase(".hidden", ".hidden")]
    [TestCase("v1.2.3", "v1.2.3")]
    [TestCase("Bushido: Coda / Part 2", "Bushido Coda Part 2")]
    [TestCase("It’s Kyle's", "Its Kyles")]
    [TestCase("名誉 — 礼", "名誉 — 礼")]
    [TestCase("emoji 🙂 ok", "emoji 🙂 ok")]
    [TestCase("CON.txt", "_CON.txt")]
    [TestCase("con .", "_con")]
    [TestCase("COM1 ", "_COM1")]
    [TestCase("<CON>", "_CON")]
    public void SanitizeTitle_known_values(string title, string expected) =>
        Assert.That(ExportPaths.SanitizeTitle(title), Is.EqualTo(expected));

    [Test]
    public void SanitizeTitle_null_is_untitled() => Assert.That(ExportPaths.SanitizeTitle(null!), Is.EqualTo("untitled"));

    public static IEnumerable<TestCaseData> RandomTitles() =>
        Enumerable.Range(0, 400).Select(s => new TestCaseData(s).SetName($"SanitizeTitle_invariants(seed {s})"));

    [TestCaseSource(nameof(RandomTitles))]
    public void SanitizeTitle_invariants(int seed)
    {
        var g = new TextGen(seed);
        var title = g.Chance(0.5) ? g.Title(6) : g.MarkerSoup(30) + string.Concat(Enumerable.Range(0, g.R.Next(4)).Select(_ => g.Pick(WindowsInvalid)));
        var result = ExportPaths.SanitizeTitle(title);
        Assert.That(result, Is.Not.Empty);
        Assert.That(result.IndexOfAny([.. WindowsInvalid, '\'', '’']), Is.EqualTo(-1));
        Assert.That(result.Any(char.IsControl), Is.False);
        Assert.That(result, Does.Not.Contain("  "));
        Assert.That(result, Does.Not.EndWith("."));
        Assert.That(result, Does.Not.EndWith(" "));
        Assert.That(result, Does.Not.StartWith(" "));
        Assert.That(ExportPaths.IsReservedDeviceName(result), Is.False);
        Assert.That(ExportPaths.SanitizeTitle(result), Is.EqualTo(result), "idempotent");
    }

    [TestCase("1381", 13, "docx", "1381 V13.docx")]
    [TestCase("1381", 13, ".docx", "1381 V13.docx")]
    [TestCase("SET", 1, "pdf", "SET V1.pdf")]
    [TestCase("My Book", 0, "md", "My Book V0.md")]
    [TestCase("x", 999, "..epub", "x V999.epub")]
    [TestCase("x", -1, "txt", "x V-1.txt")]
    public void BundleFileName(string baseName, int version, string ext, string expected) =>
        Assert.That(ExportPaths.BundleFileName(baseName, version, ext), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> BundleRoundTrip() =>
        from v in new[] { 1, 2, 9, 10, 13, 99, 100, 12345 }
        from ext in new[] { "docx", "epub", "pdf", "txt", "md" }
        from name in new[] { "SET", "1381", "My Book" }
        select new TestCaseData(name, v, ext).SetName($"BundleFileName_round_trips_version({name}, {v}, {ext})");

    [TestCaseSource(nameof(BundleRoundTrip))]
    public void BundleFileName_round_trips_version(string name, int version, string ext) =>
        Assert.That(ExportArchive.ExtractVersion(ExportPaths.BundleFileName(name, version, ext)), Is.EqualTo(version));
}
