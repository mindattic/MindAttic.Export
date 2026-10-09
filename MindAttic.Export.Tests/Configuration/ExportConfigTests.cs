using System.Text.Json;
using MindAttic.Export.Configuration;
using MindAttic.Export.Markdown;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Configuration;

[TestFixture]
public class ExportConfigTests : TempDirTestBase
{
    private string Write(string json, string name = "export.json")
    {
        var path = Temp.File(name);
        File.WriteAllText(path, json);
        return path;
    }

    private const string Minimal = """{ "title": "T", "outputDirectory": "out", "fileBaseName": "B" }""";

    [Test]
    public void Minimal_config_uses_document_defaults()
    {
        var c = ExportConfig.Load(Write(Minimal));
        Assert.That((c.Title, c.OutputDirectory, c.FileBaseName), Is.EqualTo(("T", "out", "B")));
        Assert.That(c.Formats, Is.EqualTo(new[] { "docx", "pdf", "txt", "md" }));
        Assert.That(c.Sources, Is.Empty);
        Assert.That(c.Keywords, Is.Empty);
        Assert.That((c.Version, c.Archive, c.Sidecars, c.Toc, c.DocxPageNumbers, c.Math), Is.EqualTo(((int?)null, true, true, true, true, true)));
        Assert.That((c.Profile, c.FontFamily, c.HeadingStyle), Is.EqualTo(("letter", "Garamond", "em-dash")));
        var o = c.ToOptions();
        Assert.That(o.Profile, Is.EqualTo(PageProfile.Letter));
        Assert.That((o.IncludeToc, o.DocxPageNumbers, o.FontFamily), Is.EqualTo((true, true, "Garamond")));
        Assert.That(o.BookIdentifier, Is.Null);
        Assert.That(o.FixedTimestamp, Is.Null);
    }

    [Test]
    public void Comments_trailing_commas_and_any_property_case()
    {
        var c = ExportConfig.Load(Write("""
            {
              // line comment
              "TITLE": "Synthetic Exchange Theory", /* block comment */
              "Subtitle": "A Dissertation",
              "author": "R",
              "description": "D",
              "keywords": ["a", "b",],
              "sources": ["chapters/*.md", "refs.md",],
              "outputDirectory": "out",
              "FileBaseName": "SET",
              "formats": ["docx", "md"],
              "version": 7,
              "archive": false,
              "sidecars": false,
              "profile": "trade",
              "toc": false,
              "docxPageNumbers": false,
              "fontFamily": "Georgia",
              "headingStyle": "as-written",
              "hangingIndentChapters": ["Sources"],
              "math": false,
            }
            """));
        Assert.That((c.Title, c.Subtitle, c.Author, c.Description), Is.EqualTo(("Synthetic Exchange Theory", "A Dissertation", "R", "D")));
        Assert.That(c.Keywords, Is.EqualTo(new[] { "a", "b" }));
        Assert.That(c.Sources, Is.EqualTo(new[] { "chapters/*.md", "refs.md" }));
        Assert.That(c.Formats, Is.EqualTo(new[] { "docx", "md" }));
        Assert.That((c.Version, c.Archive, c.Sidecars), Is.EqualTo(((int?)7, false, false)));
        var o = c.ToOptions();
        Assert.That((o.Profile, o.IncludeToc, o.DocxPageNumbers, o.FontFamily), Is.EqualTo((PageProfile.Trade6x9, false, false, "Georgia")));
        var r = c.ToReaderOptions();
        Assert.That(r.HeadingStyle, Is.EqualTo(ChapterHeadingStyle.AsWritten));
        Assert.That(r.HangingIndentChapters, Is.EqualTo(new[] { "Sources" }));
        Assert.That(r.Math, Is.False);
        var info = c.ToInfo();
        Assert.That((info.Title, info.Subtitle, info.Author, info.Description), Is.EqualTo(("Synthetic Exchange Theory", "A Dissertation", "R", "D")));
        Assert.That(info.Keywords, Is.EqualTo(new[] { "a", "b" }));
    }

    [TestCase("letter", PageProfile.Letter)]
    [TestCase("Letter", PageProfile.Letter)]
    [TestCase("trade", PageProfile.Trade6x9)]
    [TestCase(" TRADE ", PageProfile.Trade6x9)]
    [TestCase("trade6x9", PageProfile.Trade6x9)]
    [TestCase("Trade6x9", PageProfile.Trade6x9)]
    [TestCase("6x9", PageProfile.Trade6x9)]
    [TestCase("a4", PageProfile.Letter)]
    [TestCase("", PageProfile.Letter)]
    public void Profile_parsing(string profile, PageProfile expected)
    {
        var c = ExportConfig.Load(Write(Minimal)) with { Profile = profile };
        Assert.That(c.ToOptions().Profile, Is.EqualTo(expected));
    }

    [TestCase("em-dash", ChapterHeadingStyle.EmDash)]
    [TestCase("as-written", ChapterHeadingStyle.AsWritten)]
    [TestCase("AsWritten", ChapterHeadingStyle.AsWritten)]
    [TestCase(" As-Written ", ChapterHeadingStyle.AsWritten)]
    [TestCase("emdash", ChapterHeadingStyle.EmDash)]
    [TestCase("anything", ChapterHeadingStyle.EmDash)]
    public void Heading_style_parsing(string style, ChapterHeadingStyle expected)
    {
        var c = ExportConfig.Load(Write(Minimal)) with { HeadingStyle = style };
        Assert.That(c.ToReaderOptions().HeadingStyle, Is.EqualTo(expected));
    }

    [Test]
    public void Missing_hanging_list_uses_reader_default()
    {
        var c = ExportConfig.Load(Write(Minimal));
        Assert.That(c.ToReaderOptions().HangingIndentChapters, Is.EqualTo(new MarkdownReaderOptions().HangingIndentChapters));
    }

    [TestCase("""{ "outputDirectory": "o", "fileBaseName": "B" }""")]
    [TestCase("""{ "title": "T", "fileBaseName": "B" }""")]
    [TestCase("""{ "title": "T", "outputDirectory": "o" }""")]
    [TestCase("""{ "title": "T", "outputDirectory": "o", "fileBaseName": "B", "version": "seven" }""")]
    [TestCase("""{ "title": "T", "outputDirectory": "o", "fileBaseName": "B", """)]
    [TestCase("not json")]
    public void Invalid_config_throws_json_exception(string json) =>
        Assert.Throws<JsonException>(() => ExportConfig.Load(Write(json)));

    [Test]
    public void Null_document_throws_invalid_data() =>
        Assert.Throws<InvalidDataException>(() => ExportConfig.Load(Write("null")));

    [Test]
    public void Missing_config_file_throws() =>
        Assert.Throws<FileNotFoundException>(() => ExportConfig.Load(Temp.File("nope.json")));

    // ── sources ──────────────────────────────────────────────────────────────

    private ExportConfig WithSources(params string[] sources) => ExportConfig.Load(Write(Minimal)) with { Sources = [.. sources] };

    private void Md(params string[] relative)
    {
        foreach (var r in relative)
        {
            var path = Path.Combine(Temp.Path, r);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "# x\n");
        }
    }

    [Test]
    public void Glob_matches_are_sorted_ordinally()
    {
        Md("ch/10-ten.md", "ch/02-two.md", "ch/01-one.md", "ch/B.md", "ch/a.md", "ch/notes.txt");
        var files = WithSources("ch/*.md").ResolveSources(Temp.Path).Select(f => Path.GetFileName(f));
        Assert.That(files, Is.EqualTo(new[] { "01-one.md", "02-two.md", "10-ten.md", "B.md", "a.md" }));
    }

    [Test]
    public void Sources_keep_listed_order_and_drop_duplicates()
    {
        Md("intro.md", "ch/01.md", "ch/02.md", "refs.md");
        var files = WithSources("intro.md", "ch/*.md", "ch/01.md", "refs.md", "INTRO.md").ResolveSources(Temp.Path)
            .Select(f => Path.GetRelativePath(Temp.Path, f).Replace('\\', '/'));
        Assert.That(files, Is.EqualTo(new[] { "intro.md", "ch/01.md", "ch/02.md", "refs.md" }));
    }

    [Test]
    public void Question_mark_wildcard()
    {
        Md("p1.md", "p2.md", "p10.md");
        var files = WithSources("p?.md").ResolveSources(Temp.Path).Select(f => Path.GetFileName(f));
        Assert.That(files, Is.EqualTo(new[] { "p1.md", "p2.md" }));
    }

    [Test]
    public void Absolute_sources_are_used_as_is()
    {
        Md("abs.md");
        var files = WithSources(Temp.File("abs.md")).ResolveSources(Path.GetTempPath());
        Assert.That(files, Is.EqualTo(new[] { Path.GetFullPath(Temp.File("abs.md")) }));
    }

    [Test]
    public void Paths_are_full_and_normalised()
    {
        Md("a/b.md");
        var files = WithSources("a/../a/./b.md").ResolveSources(Temp.Path);
        Assert.That(files.Single(), Is.EqualTo(Path.GetFullPath(Path.Combine(Temp.Path, "a", "b.md"))));
    }

    [Test]
    public void Missing_file_throws_with_its_path()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => WithSources("missing.md").ResolveSources(Temp.Path));
        Assert.That(ex!.FileName, Is.EqualTo(Path.Combine(Temp.Path, "missing.md")));
    }

    [Test]
    public void Glob_in_missing_folder_throws() =>
        Assert.Throws<DirectoryNotFoundException>(() => WithSources("nope/*.md").ResolveSources(Temp.Path));

    [Test]
    public void Glob_with_no_matches_is_empty()
    {
        Directory.CreateDirectory(Temp.Sub("empty"));
        Assert.That(WithSources("empty/*.md").ResolveSources(Temp.Path), Is.Empty);
    }
}

[TestFixture]
public class ExportFormatExtensionsTests
{
    [TestCase(ExportFormat.Docx, "docx")]
    [TestCase(ExportFormat.Epub, "epub")]
    [TestCase(ExportFormat.Pdf, "pdf")]
    [TestCase(ExportFormat.Txt, "txt")]
    [TestCase(ExportFormat.Md, "md")]
    public void Extension(ExportFormat format, string ext) => Assert.That(format.Extension(), Is.EqualTo(ext));

    [Test]
    public void Extension_of_undefined_value_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ExportFormat)99).Extension());

    public static IEnumerable<TestCaseData> ParseCases()
    {
        foreach (var f in Enum.GetValues<ExportFormat>())
        {
            var ext = f.Extension();
            foreach (var v in new[] { ext, ext.ToUpperInvariant(), "." + ext, $"  {ext} ", $" .{ext.ToUpperInvariant()}", "..." + ext, char.ToUpperInvariant(ext[0]) + ext[1..] })
                yield return new TestCaseData(v, f).SetName($"Parse(\"{v}\")");
        }
        yield return new TestCaseData("markdown", ExportFormat.Md).SetName("Parse(\"markdown\")");
        yield return new TestCaseData("Markdown", ExportFormat.Md).SetName("Parse(\"Markdown\")");
        yield return new TestCaseData(".MARKDOWN", ExportFormat.Md).SetName("Parse(\".MARKDOWN\")");
    }

    [TestCaseSource(nameof(ParseCases))]
    public void Parse(string value, ExportFormat expected) => Assert.That(ExportFormatExtensions.Parse(value), Is.EqualTo(expected));

    [TestCase("")]
    [TestCase("doc")]
    [TestCase("html")]
    [TestCase("text")]
    [TestCase("d o c x")]
    [TestCase("docx,pdf")]
    [TestCase("mobi")]
    public void Parse_unknown_throws(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => ExportFormatExtensions.Parse(value));
        Assert.That(ex!.Message, Does.Contain(value));
    }

    [Test]
    public void Parse_round_trips_every_extension()
    {
        foreach (var f in Enum.GetValues<ExportFormat>())
            Assert.That(ExportFormatExtensions.Parse(f.Extension()), Is.EqualTo(f));
    }

    [Test]
    public void Presets()
    {
        Assert.That(ExportOptions.ProseBook, Is.EqualTo(new ExportOptions()));
        Assert.That(ExportOptions.ProseBook.Profile, Is.EqualTo(PageProfile.Trade6x9));
        Assert.That((ExportOptions.ProseBook.IncludeToc, ExportOptions.ProseBook.DocxPageNumbers), Is.EqualTo((false, false)));
        Assert.That(ExportOptions.ProseBook.FontFamily, Is.EqualTo("Garamond"));
        Assert.That(ExportOptions.Document.Profile, Is.EqualTo(PageProfile.Letter));
        Assert.That((ExportOptions.Document.IncludeToc, ExportOptions.Document.DocxPageNumbers), Is.EqualTo((true, true)));
    }
}
