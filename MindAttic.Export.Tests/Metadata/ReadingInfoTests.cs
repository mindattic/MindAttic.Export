using MindAttic.Export.Metadata;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Metadata;

[TestFixture]
public class ReadingInfoTests : TempDirTestBase
{
    [TestCase("", 0)]
    [TestCase("   ", 0)]
    [TestCase("one", 1)]
    [TestCase("one two", 2)]
    [TestCase("  one   two  ", 2)]
    [TestCase("one\ttwo\nthree\r\nfour", 4)]
    [TestCase("fear-sweat", 1)]
    [TestCase("can't stop", 2)]
    [TestCase("a — b", 3)]
    [TestCase("a b", 1)]
    [TestCase("名誉 礼", 2)]
    [TestCase("**bold** text", 2)]
    [TestCase("\n\n\n", 0)]
    public void CountWords_known(string text, int expected) =>
        Assert.That(ReadingInfo.CountWords(text), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> WordSeeds() =>
        Enumerable.Range(0, 200).Select(s => new TestCaseData(s).SetName($"CountWords_counts_generated_words(seed {s})"));

    [TestCaseSource(nameof(WordSeeds))]
    public void CountWords_counts_generated_words(int seed)
    {
        var r = new Random(seed);
        var n = r.Next(0, 300);
        var seps = new[] { " ", "  ", "\t", "\n", "\r\n", " \n " };
        var words = Enumerable.Range(0, n).Select(i => $"w{i}").ToList();
        var text = (r.Next(2) == 0 ? " " : "") + string.Join("", words.Select(w => w + seps[r.Next(seps.Length)]));
        Assert.That(ReadingInfo.CountWords(text), Is.EqualTo(n));
    }

    public static IEnumerable<TestCaseData> PageCases()
    {
        foreach (var words in new[] { 0, 1, 124, 125, 126, 249, 250, 251, 374, 375, 376, 624, 625, 626, 1000, 12345, 99999, 250000 })
            yield return new TestCaseData(words, Math.Max(1, (int)Math.Round(words / 250.0))).SetName($"KindlePages({words})");
    }

    [TestCaseSource(nameof(PageCases))]
    public void KindlePages(int words, int expected) => Assert.That(ReadingInfo.KindlePages(words), Is.EqualTo(expected));

    [TestCase(0, 1)]
    [TestCase(124, 1)]
    [TestCase(125, 1)]   // 0.5 → banker's rounding → 0 → floor 1
    [TestCase(375, 2)]   // 1.5 → 2
    [TestCase(625, 2)]   // 2.5 → 2 (banker's)
    [TestCase(875, 4)]   // 3.5 → 4
    [TestCase(250, 1)]
    [TestCase(500, 2)]
    public void KindlePages_uses_bankers_rounding(int words, int expected) =>
        Assert.That(ReadingInfo.KindlePages(words), Is.EqualTo(expected));

    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(100, 1)]   // 0.5 → 0 → 1
    [TestCase(300, 2)]   // 1.5 → 2
    [TestCase(500, 2)]   // 2.5 → 2
    [TestCase(12000, 60)]
    [TestCase(80000, 400)]
    public void ReadingMinutes(int words, int expected) => Assert.That(ReadingInfo.ReadingMinutes(words), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> MinuteCases() =>
        Enumerable.Range(0, 2001).Select(m => new TestCaseData(m).SetName($"FormatReadingTime({m})"));

    [TestCaseSource(nameof(MinuteCases))]
    public void FormatReadingTime(int minutes)
    {
        var text = ReadingInfo.FormatReadingTime(minutes);
        var h = minutes / 60;
        var m = minutes % 60;
        var expected = h > 0 && m > 0 ? $"{h} hr {m} min" : h > 0 ? $"{h} hr" : $"{m} min";
        Assert.That(text, Is.EqualTo(expected));
    }

    [TestCase(0, "Approximately 1 pages and 1 min to read.")]
    [TestCase(250, "Approximately 1 pages and 1 min to read.")]
    [TestCase(12000, "Approximately 48 pages and 1 hr to read.")]
    [TestCase(15000, "Approximately 60 pages and 1 hr 15 min to read.")]
    [TestCase(80000, "Approximately 320 pages and 6 hr 40 min to read.")]
    public void ReadingLine(int words, string expected) => Assert.That(ReadingInfo.ReadingLine(words), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> DescriptionSeeds() =>
        Enumerable.Range(0, 150).Select(s => new TestCaseData(s).SetName($"WithReadingLine_never_duplicates(seed {s})"));

    [TestCaseSource(nameof(DescriptionSeeds))]
    public void WithReadingLine_never_duplicates(int seed)
    {
        var g = new TextGen(seed, TextFeatures.Unicode | TextFeatures.Markup);
        var description = g.R.Next(4) == 0 ? "" : g.MultiParagraph(3);
        var words = g.R.Next(0, 200000);
        var once = ReadingInfo.WithReadingLine(description, words);
        var twice = ReadingInfo.WithReadingLine(once, g.R.Next(0, 200000));
        var thrice = ReadingInfo.WithReadingLine(twice, words);
        Assert.That(CountOccurrences(once, "Approximately "), Is.EqualTo(1));
        Assert.That(CountOccurrences(twice, "Approximately "), Is.EqualTo(1));
        Assert.That(thrice, Is.EqualTo(once), "re-applying with the original word count restores the original");
        Assert.That(once, Does.EndWith(ReadingInfo.ReadingLine(words)));
        Assert.That(ReadingInfo.StripReadingInfoLine(once), Is.EqualTo(ReadingInfo.StripReadingInfoLine(twice)));
        if (!string.IsNullOrWhiteSpace(description))
            Assert.That(once, Is.EqualTo(description.TrimEnd() + "\n\n" + ReadingInfo.ReadingLine(words)));
        else
            Assert.That(once, Is.EqualTo(ReadingInfo.ReadingLine(words)));
    }

    private static int CountOccurrences(string s, string sub)
    {
        var n = 0;
        for (var i = s.IndexOf(sub, StringComparison.Ordinal); i >= 0; i = s.IndexOf(sub, i + sub.Length, StringComparison.Ordinal)) n++;
        return n;
    }

    public static IEnumerable<TestCaseData> StripSeeds() =>
        Enumerable.Range(0, 150).Select(s => new TestCaseData(s).SetName($"StripReadingInfoLine_is_idempotent(seed {s})"));

    [TestCaseSource(nameof(StripSeeds))]
    public void StripReadingInfoLine_is_idempotent(int seed)
    {
        var g = new TextGen(seed, TextFeatures.Unicode);
        var body = g.MultiParagraph(3);
        var withLine = seed % 3 == 0 ? body : ReadingInfo.WithReadingLine(body, seed * 97);
        var once = ReadingInfo.StripReadingInfoLine(withLine);
        Assert.That(ReadingInfo.StripReadingInfoLine(once), Is.EqualTo(once));
        Assert.That(once, Does.Not.Contain("Approximately"));
    }

    [TestCase("Blurb.\n\nApproximately 3 pages and 4 min to read.", "Blurb.")]
    [TestCase("Blurb.\n\napproximately 1 page and 1 hr 2 min to read.  \n", "Blurb.")]
    [TestCase("Blurb. Approximately 3 pages and 4 min to read.", "Blurb.")]
    [TestCase("Approximately 3 pages and 4 min to read.", "")]
    [TestCase("Approximately 3 pages and 4 min to read. More after.", "Approximately 3 pages and 4 min to read. More after.")]
    [TestCase("No line here.", "No line here.")]
    [TestCase("Approximately many pages and 4 min to read.", "Approximately many pages and 4 min to read.")]
    public void StripReadingInfoLine_known(string input, string expected) =>
        Assert.That(ReadingInfo.StripReadingInfoLine(input), Is.EqualTo(expected));

    [Test]
    public void WithReadingLine_null_description() =>
        Assert.That(ReadingInfo.WithReadingLine(null, 500), Is.EqualTo("Approximately 2 pages and 2 min to read."));

    [Test]
    public async Task WriteDescription_trims_and_returns_path()
    {
        var path = await ReadingInfo.WriteDescriptionAsync(Temp.Path, "  \n Blurb text \n\n ");
        Assert.That(path, Is.EqualTo(Path.Combine(Temp.Path, "description.txt")));
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("Blurb text"));
    }

    [Test]
    public async Task WriteKeywords_one_per_line_or_null()
    {
        Assert.That(await ReadingInfo.WriteKeywordsAsync(Temp.Path, []), Is.Null);
        Assert.That(File.Exists(Path.Combine(Temp.Path, "keywords.txt")), Is.False);
        var path = await ReadingInfo.WriteKeywordsAsync(Temp.Path, ["sci-fi", "noir thriller", "AI"]);
        Assert.That(path, Is.EqualTo(Path.Combine(Temp.Path, "keywords.txt")));
        Assert.That(await File.ReadAllLinesAsync(path!), Is.EqualTo(new[] { "sci-fi", "noir thriller", "AI" }));
    }
}
