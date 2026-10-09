using System.Text;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Support;
using ProseLegacy;

namespace MindAttic.Export.Tests.LegacyEquivalence;

/// <summary>
/// The migration guard: for hundreds of seeded random Prose books, the new renderers (fed through
/// <see cref="ProseAdapter"/>) must reproduce the frozen legacy exporters in
/// <c>Legacy\</c> exactly.
/// </summary>
[TestFixture]
public class LegacyEquivalenceTests : TempDirTestBase
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static IEnumerable<TestCaseData> Seeds(int start, int count, string family) =>
        Enumerable.Range(start, count).Select(s => new TestCaseData(s).SetName($"{family}(seed {s})"));

    public static IEnumerable<TestCaseData> TxtSeeds() => Seeds(1, 400, "Txt_matches_legacy_byte_for_byte");
    public static IEnumerable<TestCaseData> TxtBlankHeadingSeeds() => Seeds(5001, 150, "Txt_with_blank_headings_matches_legacy");
    public static IEnumerable<TestCaseData> MdSeeds() => Seeds(1001, 400, "Md_matches_legacy_byte_for_byte");
    public static IEnumerable<TestCaseData> EpubSeeds() => Seeds(2001, 300, "Epub_entries_match_legacy");
    public static IEnumerable<TestCaseData> EpubBlankHeadingSeeds() => Seeds(6001, 100, "Epub_with_blank_headings_matches_legacy");
    public static IEnumerable<TestCaseData> DocxSeeds() => Seeds(3001, 300, "Docx_parts_match_legacy");
    public static IEnumerable<TestCaseData> PdfSeeds() => Seeds(4001, 40, "Pdf_matches_legacy_after_metadata_normalisation");

    // ── TXT ──────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(TxtSeeds))]
    public async Task Txt(int seed) => await AssertTxt(LegacyBookGen.Generate(seed));

    [TestCaseSource(nameof(TxtBlankHeadingSeeds))]
    public async Task TxtBlankHeadings(int seed) => await AssertTxt(LegacyBookGen.Generate(seed, BookGenOptions.LoadAsync));

    private async Task AssertTxt(LegacyBook book)
    {
        var expected = LegacyManuscriptExport.ExportAudioTxt(book);
        var manuscript = ProseAdapter.ForLoad(book);
        Assert.That(TextRenderer.Render(manuscript), Is.EqualTo(expected), book.ToString());

        // The file on disk too: UTF-8 without BOM, byte for byte.
        var path = Temp.File("book.txt");
        await new TextRenderer().RenderAsync(manuscript, path, ExportOptions.ProseBook);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(Utf8NoBom.GetBytes(expected)));
        // Tenets as plain paragraphs (how LoadAsync saw them) give the same text.
        Assert.That(TextRenderer.Render(ProseAdapter.ForLoad(book, tenetBlocks: false)), Is.EqualTo(expected));
    }

    // ── MD ───────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(MdSeeds))]
    public async Task Md(int seed)
    {
        var book = LegacyBookGen.Generate(seed);
        var expected = LegacyManuscriptExport.ExportMarkdown(book);
        var manuscript = ProseAdapter.ForMarkdown(book);
        Assert.That(MarkdownRenderer.Render(manuscript), Is.EqualTo(expected), book.ToString());

        var path = Temp.File("book.md");
        await new MarkdownRenderer().RenderAsync(manuscript, path, ExportOptions.ProseBook);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(Utf8NoBom.GetBytes(expected)));
        Assert.That(MarkdownRenderer.Render(ProseAdapter.ForMarkdown(book, tenetBlocks: false)), Is.EqualTo(expected));
    }

    // ── EPUB ─────────────────────────────────────────────────────────────────

    private static readonly LegacySeams Seams = LegacySeams.Fixed;

    private static ExportOptions EpubOptions => ExportOptions.ProseBook with
    {
        BookIdentifier = $"urn:uuid:{Seams.BookUuid}",
        FixedTimestamp = Seams.UtcNow
    };

    [TestCaseSource(nameof(EpubSeeds))]
    public void Epub(int seed) => AssertEpub(LegacyBookGen.Generate(seed));

    [TestCaseSource(nameof(EpubBlankHeadingSeeds))]
    public void EpubBlankHeadings(int seed) => AssertEpub(LegacyBookGen.Generate(seed, BookGenOptions.LoadAsync));

    private void AssertEpub(LegacyBook book)
    {
        var legacyPath = Temp.File("legacy.epub");
        var newPath = Temp.File("new.epub");
        LegacyManuscriptExport.ExportEpub(book, legacyPath, Seams);
        EpubRenderer.Render(ProseAdapter.ForLoad(book), newPath, EpubOptions);

        var legacy = Packages.ReadZip(legacyPath);
        var current = Packages.ReadZip(newPath);
        Assert.That(current.Select(e => e.Name), Is.EqualTo(legacy.Select(e => e.Name)), "entry names and order");
        Assert.That(Packages.FirstLocalEntry(newPath).Name, Is.EqualTo("mimetype"));
        Assert.That(Packages.FirstLocalEntry(newPath).Method, Is.EqualTo(Packages.FirstLocalEntry(legacyPath).Method));
        Assert.That(Packages.FirstLocalEntry(newPath).Method, Is.EqualTo((ushort)0), "mimetype stored");
        for (var i = 0; i < legacy.Count; i++)
        {
            Assert.That(current[i].Method, Is.EqualTo(legacy[i].Method), $"compression method of {legacy[i].Name}");
            Assert.That(current[i].Text, Is.EqualTo(legacy[i].Text), $"{legacy[i].Name} ({book})");
            Assert.That(current[i].Content, Is.EqualTo(legacy[i].Content), legacy[i].Name);
        }
    }

    [Test]
    public void Epub_document_stylesheet_is_the_legacy_book_stylesheet_plus_document_rules()
    {
        // A Prose book's styles.css is the legacy one exactly (checked in every seed above); a
        // manuscript with document blocks gets the legacy stylesheet with the document rules
        // appended, never a modified copy.
        var book = LegacyBookGen.Generate(1);
        var legacyPath = Temp.File("legacy.epub");
        var newPath = Temp.File("new.epub");
        LegacyManuscriptExport.ExportEpub(book, legacyPath, Seams);
        var manuscript = ProseAdapter.ForLoad(book);
        manuscript.Chapters[^1].Blocks.Add(new MindAttic.Export.Model.RuleBlock());
        EpubRenderer.Render(manuscript, newPath, EpubOptions);
        var legacy = Packages.ReadZip(legacyPath).Single(e => e.Name == "OEBPS/styles.css").Text;
        var current = Packages.ReadZip(newPath).Single(e => e.Name == "OEBPS/styles.css").Text;
        Assert.That(current, Does.StartWith(legacy));
        var extra = current[legacy.Length..].Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
        Assert.That(extra, Has.Count.EqualTo(9), string.Join("\n", extra));
    }

    // ── DOCX ─────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(DocxSeeds))]
    public void Docx(int seed)
    {
        var book = LegacyBookGen.Generate(seed);
        var legacyPath = Temp.File("legacy.docx");
        var newPath = Temp.File("new.docx");
        var (manuscript, options) = ProseAdapter.ForDocx(book);
        int legacyWords, legacyPages;
        try
        {
            (legacyWords, legacyPages) = LegacyDocxExport.Export(book, legacyPath);
        }
        catch (ArgumentException legacyError)
        {
            // Legacy defect, fixed in the library (3.0.0): an XML-illegal control character in the
            // author crashed the package-properties writer. The library scrubs it, as it does every
            // run, so the new render succeeds where legacy threw.
            Assert.That(legacyError, Is.Not.Null);
            Assert.That(book.Author, Does.Match(@"[\x00-\x08\x0B\x0C\x0E-\x1F]"));
            Assert.DoesNotThrow(() => DocxRenderer.Render(manuscript, newPath, options));
            return;
        }
        var result = DocxRenderer.Render(manuscript, newPath, options);

        Assert.That(result.WordCount, Is.EqualTo(legacyWords), "wordCount");
        Assert.That(result.EstimatedPages, Is.EqualTo(legacyPages), "estimatedPages");
        Assert.That(result.Path, Is.EqualTo(newPath));

        var legacy = Packages.ReadZip(legacyPath);
        var current = Packages.ReadZip(newPath);
        Assert.That(current.Select(e => DocxCompare.PartName(e.Name)), Is.EqualTo(legacy.Select(e => DocxCompare.PartName(e.Name))), "package parts and order");
        foreach (var part in legacy)
        {
            var other = current.Single(e => DocxCompare.PartName(e.Name) == DocxCompare.PartName(part.Name));
            Assert.That(DocxCompare.Normalise(other), Is.EqualTo(DocxCompare.Normalise(part)), $"{part.Name} ({book})");
        }

        using var a = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(legacyPath, false);
        using var b = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(newPath, false);
        Assert.That(b.PackageProperties.Creator, Is.EqualTo(a.PackageProperties.Creator));
        Assert.That(b.PackageProperties.LastModifiedBy, Is.EqualTo(a.PackageProperties.LastModifiedBy));
        Assert.That(b.PackageProperties.Creator, Is.EqualTo(ProseAdapter.ResolveAuthor(book.Author)));
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(PdfSeeds))]
    public void Pdf_(int seed)
    {
        var book = LegacyBookGen.Generate(seed, new BookGenOptions { BlankHeadings = true, MaxChapters = 6 });
        var legacyPath = Temp.File("legacy.pdf");
        var newPath = Temp.File("new.pdf");
        LegacyManuscriptExport.ExportPdf(book, legacyPath);
        PdfRenderer.Render(ProseAdapter.ForLoad(book), newPath, ExportOptions.ProseBook);

        var legacy = File.ReadAllBytes(legacyPath);
        var current = File.ReadAllBytes(newPath);
        Assert.That(Support.Pdf.LooksValid(current), Is.True);
        Assert.That(Support.Pdf.PageCount(current), Is.EqualTo(Support.Pdf.PageCount(legacy)), "page count");
        Assert.That(current, Has.Length.EqualTo(legacy.Length), "byte length");
        Assert.That(Support.Pdf.Normalised(current), Is.EqualTo(Support.Pdf.Normalised(legacy)), book.ToString());
    }
}
