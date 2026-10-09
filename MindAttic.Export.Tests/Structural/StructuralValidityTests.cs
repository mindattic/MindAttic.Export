using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using MindAttic.Export.Markdown;
using MindAttic.Export.Model;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.LegacyEquivalence;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Structural;

/// <summary>
/// Every format, every page profile, TOC on/off and docx page numbers on/off, over seeded
/// document-style manuscripts (headings, tables, lists, quotes, math, reference chapters, code)
/// and seeded Prose books: the files must be structurally valid.
/// </summary>
[TestFixture]
public class StructuralValidityTests : TempDirTestBase
{
    private const int Documents = 16;

    public static Manuscript DocumentManuscript(int seed)
    {
        var doc = DocumentGen.Generate(seed);
        var m = MarkdownManuscriptReader.Read(doc.Markdown, new ManuscriptInfo
        {
            Title = $"Document {seed}",
            Subtitle = seed % 2 == 0 ? "A Report" : null,
            Author = seed % 3 == 0 ? null : "MindAttic",
            Description = "Catalog blurb & <notes>.",
            Keywords = ["a", "b"]
        });
        if (seed % 4 == 0)
            m.Glossary.Add(new GlossaryEntry("SET", "Synthetic Exchange Theory", "The theory under test."));
        return m;
    }

    private static ExportOptions Options(PageProfile profile, bool toc, bool pageNumbers) => new()
    {
        Profile = profile,
        IncludeToc = toc,
        DocxPageNumbers = pageNumbers,
        BookIdentifier = "urn:uuid:11111111-2222-3333-4444-555555555555",
        FixedTimestamp = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)
    };

    public static IEnumerable<TestCaseData> Matrix(string name, bool withPageNumbers = true)
    {
        for (var seed = 0; seed < Documents; seed++)
            foreach (var profile in new[] { PageProfile.Trade6x9, PageProfile.Letter })
                foreach (var toc in new[] { false, true })
                    foreach (var numbers in withPageNumbers ? new[] { false, true } : [false])
                        yield return new TestCaseData(seed, profile, toc, numbers)
                            .SetName($"{name}(doc {seed}, {profile}, toc={toc}{(withPageNumbers ? $", pageNumbers={numbers}" : "")})");
    }

    public static IEnumerable<TestCaseData> DocxMatrix() => Matrix("Docx_opens_and_validates");
    public static IEnumerable<TestCaseData> EpubMatrix() => Matrix("Epub_is_well_formed");
    public static IEnumerable<TestCaseData> PdfMatrix() => Matrix("Pdf_is_a_complete_pdf", withPageNumbers: false);

    // ── DOCX ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Schema-order defects the docx writer has today (see the ignored tests below). Each is
    /// (parent element, unexpected child). Anything outside this list fails the matrix.
    /// </summary>
    internal static readonly HashSet<(string Parent, string Child)> KnownOrderDefects = [];

    internal static List<ValidationErrorInfo> Validate(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        Assert.That(doc.MainDocumentPart?.Document?.Body, Is.Not.Null);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToList();
        foreach (var e in errors) _ = e.Path?.XPath; // resolved lazily; read it while the package is still open
        return errors;
    }

    internal static (string Parent, string Child)? OrderDefect(ValidationErrorInfo e)
    {
        if (e.Id != "Sch_UnexpectedElementContentExpectingComplex") return null;
        var parent = e.Node?.LocalName;
        var m = System.Text.RegularExpressions.Regex.Match(e.Description, @"unexpected child element '[^']*:(\w+)'");
        return parent is null || !m.Success ? null : (parent, m.Groups[1].Value);
    }

    internal static void AssertOnlyKnownDefects(List<ValidationErrorInfo> errors)
    {
        var unknown = errors.Where(e => OrderDefect(e) is not { } d || !KnownOrderDefects.Contains(d)).ToList();
        Assert.That(unknown, Is.Empty, string.Join("\n", unknown.Take(10).Select(e => $"{e.Id} {e.Path?.XPath}: {e.Description}")));
    }

    [TestCaseSource(nameof(DocxMatrix))]
    public void Docx(int seed, PageProfile profile, bool toc, bool pageNumbers)
    {
        var m = DocumentManuscript(seed);
        var path = Temp.File("doc.docx");
        var result = DocxRenderer.Render(m, path, Options(profile, toc, pageNumbers));
        Assert.That(result.WordCount, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.EstimatedPages, Is.GreaterThanOrEqualTo(1));
        AssertOnlyKnownDefects(Validate(path));

        using var doc = WordprocessingDocument.Open(path, false);
        var main = doc.MainDocumentPart!;
        var body = main.Document!.Body!;
        var sect = body.Elements<DocumentFormat.OpenXml.Wordprocessing.SectionProperties>().Single();
        var size = sect.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.PageSize>()!;
        Assert.That(((uint)size.Width!, (uint)size.Height!), Is.EqualTo(profile == PageProfile.Trade6x9 ? (8640u, 12960u) : (12240u, 15840u)));
        Assert.That(main.FooterParts.Count(), Is.EqualTo(pageNumbers ? 1 : 0));
        Assert.That(sect.Elements<DocumentFormat.OpenXml.Wordprocessing.FooterReference>().Count(), Is.EqualTo(pageNumbers ? 1 : 0));
        var headed = m.Chapters.Count(c => !string.IsNullOrWhiteSpace(c.Heading));
        var hasToc = body.Elements<DocumentFormat.OpenXml.Wordprocessing.SdtBlock>().Any();
        Assert.That(hasToc, Is.EqualTo(toc && headed >= 2));
        Assert.That(main.DocumentSettingsPart!.Settings!.Elements<DocumentFormat.OpenXml.Wordprocessing.MirrorMargins>().Any(), Is.EqualTo(profile == PageProfile.Trade6x9));
        // Bookmark ids are unique and every TOC hyperlink anchor resolves to a bookmark.
        var bookmarks = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.BookmarkStart>().ToList();
        Assert.That(bookmarks.Select(b => b.Id!.Value), Is.Unique);
        var names = bookmarks.Select(b => b.Name!.Value).ToHashSet();
        foreach (var link in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Hyperlink>())
            Assert.That(names, Does.Contain(link.Anchor!.Value));
    }

    public static IEnumerable<TestCaseData> BookSeeds() =>
        from seed in Enumerable.Range(7001, 40)
        from profile in new[] { PageProfile.Trade6x9, PageProfile.Letter }
        select new TestCaseData(seed, profile).SetName($"Prose_book_docx_validates(seed {seed}, {profile})");

    [TestCaseSource(nameof(BookSeeds))]
    public void Prose_book_docx_validates(int seed, PageProfile profile)
    {
        var book = LegacyBookGen.Generate(seed, new BookGenOptions { Features = TextFeatures.All & ~TextFeatures.Controls }) with { IncludeToc = true };
        var (m, o) = ProseAdapter.ForDocx(book);
        var path = Temp.File("book.docx");
        DocxRenderer.Render(m, path, o with { Profile = profile, DocxPageNumbers = seed % 2 == 0 });
        AssertOnlyKnownDefects(Validate(path));
    }

    [Test]
    public void Prose_book_docx_has_zero_schema_errors()
    {
        var book = LegacyBookGen.Generate(7001);
        var (m, o) = ProseAdapter.ForDocx(book);
        var path = Temp.File("book.docx");
        DocxRenderer.Render(m, path, o);
        Assert.That(Validate(path), Is.Empty);
    }

    [Test]
    public void Document_blocks_docx_have_zero_schema_errors()
    {
        var m = new Manuscript
        {
            Title = "T",
            Chapters =
            [
                new Chapter("C", [
                    new TableBlock([new Model.TableRow(true, [new ParagraphBlock("a")]), new Model.TableRow(false, [new ParagraphBlock("b")])]),
                    new ParagraphBlock("line 1\nline 2", [new MindAttic.Export.Text.ProseInline.Span("line 1\nline 2", MindAttic.Export.Text.ProseInline.Style.Code)], ParagraphRole.Preformatted)
                ])
            ]
        };
        var path = Temp.File("doc.docx");
        DocxRenderer.Render(m, path, ExportOptions.Document);
        var errors = Validate(path).Where(e => OrderDefect(e) is ("tblBorders", _) or ("r", "rPr")).ToList();
        Assert.That(errors, Is.Empty);
    }

    // ── EPUB ─────────────────────────────────────────────────────────────────

    private static XDocument ParseXml(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        return XDocument.Load(reader);
    }

    [TestCaseSource(nameof(EpubMatrix))]
    public void Epub(int seed, PageProfile profile, bool toc, bool pageNumbers)
    {
        var m = DocumentManuscript(seed);
        var path = Temp.File("doc.epub");
        EpubRenderer.Render(m, path, Options(profile, toc, pageNumbers));
        AssertEpub(path, m);
    }

    internal static void AssertEpub(string path, Manuscript m)
    {
        var first = Packages.FirstLocalEntry(path);
        Assert.That(first.Name, Is.EqualTo("mimetype"));
        Assert.That(first.Method, Is.EqualTo((ushort)0), "mimetype stored");
        var entries = Packages.ReadZip(path);
        Assert.That(entries[0].Text, Is.EqualTo("application/epub+zip"));
        Assert.That(entries.Select(e => e.Name), Does.Contain("META-INF/container.xml"));
        var container = ParseXml(entries.Single(e => e.Name == "META-INF/container.xml").Text);
        var opfPath = container.Descendants().Single(e => e.Name.LocalName == "rootfile").Attribute("full-path")!.Value;
        var opf = ParseXml(entries.Single(e => e.Name == opfPath).Text);
        foreach (var entry in entries.Where(e => e.Name.EndsWith(".xhtml", StringComparison.Ordinal)))
        {
            Assert.That(entry.Content.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "no BOM");
            Assert.DoesNotThrow(() => ParseXml(entry.Text), entry.Name);
        }
        // Manifest and spine agree with the package.
        var hrefs = opf.Descendants().Where(e => e.Name.LocalName == "item").Select(e => "OEBPS/" + e.Attribute("href")!.Value).ToList();
        Assert.That(hrefs, Is.EquivalentTo(entries.Select(e => e.Name).Where(n => n.StartsWith("OEBPS/", StringComparison.Ordinal) && n != opfPath)));
        var chapters = EpubChapterCount(m);
        Assert.That(entries.Count(e => e.Name.StartsWith("OEBPS/chapter-", StringComparison.Ordinal)), Is.EqualTo(chapters));
        Assert.That(opf.Descendants().Count(e => e.Name.LocalName == "itemref"), Is.EqualTo(chapters + 2));
        Assert.That(opf.Descendants().Single(e => e.Name.LocalName == "title").Value, Is.EqualTo(m.Title));
    }

    private static int EpubChapterCount(Manuscript m) => m.Chapters.Count + (m.Glossary.Count > 0 ? 1 : 0);

    // ── PDF ──────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(PdfMatrix))]
    public void Pdf(int seed, PageProfile profile, bool toc, bool pageNumbers)
    {
        var m = DocumentManuscript(seed);
        var path = Temp.File("doc.pdf");
        PdfRenderer.Render(m, path, Options(profile, toc, pageNumbers));
        var bytes = File.ReadAllBytes(path);
        Assert.That(Support.Pdf.LooksValid(bytes), Is.True);
        var text = Support.Pdf.Latin1(bytes);
        Assert.That(text, Does.StartWith("%PDF-"));
        Assert.That(text.TrimEnd(), Does.EndWith("%%EOF"));
        // Title page, one page per chapter (plus the glossary chapter), and the contents page.
        var chapters = EpubChapterCount(m);
        var headed = m.Chapters.Count(c => !string.IsNullOrWhiteSpace(c.Heading)) + (m.Glossary.Count > 0 ? 1 : 0);
        var minimumPages = 1 + chapters + (toc && headed >= 2 ? 1 : 0);
        Assert.That(Support.Pdf.PageCount(bytes), Is.GreaterThanOrEqualTo(minimumPages));
    }

    // ── MD and TXT ───────────────────────────────────────────────────────────

    public static IEnumerable<TestCaseData> DocSeeds(string name) =>
        Enumerable.Range(0, 60).Select(s => new TestCaseData(s).SetName($"{name}(doc {s})"));

    public static IEnumerable<TestCaseData> MdSeeds() => DocSeeds("Md_reads_back_with_the_same_chapter_headings");
    public static IEnumerable<TestCaseData> TxtSeeds() => DocSeeds("Txt_has_no_markdown_markers");

    [TestCaseSource(nameof(MdSeeds))]
    public void Md(int seed)
    {
        var m = DocumentManuscript(seed);
        var md = MarkdownRenderer.Render(m);
        // The renderer writes "# Title" and each chapter as "## Heading" (document headings go one
        // level deeper, from ###), so reading it back gives the title as the only level-1 chapter
        // and the chapter headings as the level-2 headings, in order.
        var back = MarkdownManuscriptReader.ReadChapters(md, new MarkdownReaderOptions { HeadingStyle = ChapterHeadingStyle.AsWritten });
        Assert.That(back[0].Heading, Is.EqualTo(m.Title));
        var level2 = back.SelectMany(c => c.Blocks).OfType<HeadingBlock>().Where(h => h.Level == 2).Select(h => h.Text);
        Assert.That(level2, Is.EqualTo(m.Chapters.Where(c => !string.IsNullOrWhiteSpace(c.Heading)).Select(c => c.Heading)));
        // Report mode reads the same file with the title on the title page.
        var report = MarkdownManuscriptReader.ReadReport(md, "fallback");
        Assert.That(report.Title, Is.EqualTo(m.Title));
        Assert.That(back.SelectMany(c => c.Blocks).OfType<TableBlock>().Count(), Is.EqualTo(m.Chapters.SelectMany(c => c.Blocks).OfType<TableBlock>().Count()));
    }

    [TestCaseSource(nameof(TxtSeeds))]
    public void Txt(int seed)
    {
        var m = DocumentManuscript(seed);
        var txt = TextRenderer.Render(m);
        // "* * *" is the scene-break line a thematic break renders as, not a marker.
        var prose = string.Join("\n", txt.Split('\n').Where(l => l.Trim() != "* * *"));
        foreach (var marker in new[] { "*", "~~", "`", "<u>", "</u>", "$", "\\frac", "](" })
            Assert.That(prose, Does.Not.Contain(marker), marker);
        Assert.That(txt, Does.EndWith("\n"));
        Assert.That(txt, Does.Not.EndWith("\n\n"));
        foreach (var chapter in m.Chapters.Where(c => c.Heading is not null))
            Assert.That(txt, Does.Contain(chapter.Heading));
    }
}
