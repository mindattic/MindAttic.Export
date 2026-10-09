using System.Text.RegularExpressions;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Support;
using ProseLegacy;

namespace MindAttic.Export.Tests.LegacyEquivalence;

/// <summary>
/// Inputs on which the new renderers deliberately (or unavoidably) differ from the legacy
/// exporters. Each test pins the exact difference so it cannot widen unnoticed. The seeded
/// generator steers around these shapes; see the final report for diagnosis.
/// </summary>
[TestFixture]
public class KnownDivergenceTests : TempDirTestBase
{
    private static readonly Regex PageBreak = new("<w:br w:type=\"page\"\\s*/>");
    private static readonly Regex Heading1 = new("<w:pStyle w:val=\"Heading1\"\\s*/>");

    private static string DocumentXml(string path) =>
        Packages.ReadZip(path).Single(e => e.Name == "word/document.xml").Text;

    private static LegacyBeat Beat(int n, string? text, string? sub = null, string? kind = null) =>
        new(new Guid(n, 0, 0, new byte[8]), text, sub, kind);

    [Test]
    public void Docx_blank_heading_in_multi_chapter_book_legacy_prints_empty_heading_new_skips_it()
    {
        // Unreachable from Prose: BookSpineService always resolves a non-empty heading.
        var book = new LegacyBook
        {
            Title = "T",
            Chapters = [new LegacyChapter("One", [Beat(1, "a")]), new LegacyChapter("  ", [Beat(2, "b")])]
        };
        LegacyDocxExport.Export(book, Temp.File("legacy.docx"));
        var (m, o) = ProseAdapter.ForDocx(book);
        DocxRenderer.Render(m, Temp.File("new.docx"), o);
        Assert.That(Heading1.Matches(DocumentXml(Temp.File("legacy.docx"))), Has.Count.EqualTo(2));
        Assert.That(Heading1.Matches(DocumentXml(Temp.File("new.docx"))), Has.Count.EqualTo(1));
    }

    [Test]
    public void Md_blank_heading_in_multi_chapter_book_legacy_prints_bare_hashes_new_skips_it()
    {
        var book = new LegacyBook
        {
            Title = "T",
            Chapters = [new LegacyChapter("One", [Beat(1, "a")]), new LegacyChapter(null, [Beat(2, "b")])]
        };
        // Both writers use AppendLine, i.e. Environment.NewLine line endings.
        var bare = Environment.NewLine + "## " + Environment.NewLine;
        Assert.That(LegacyManuscriptExport.ExportMarkdown(book), Does.Contain(bare));
        Assert.That(MarkdownRenderer.Render(ProseAdapter.ForMarkdown(book)), Does.Not.Contain(bare));
    }

    [Test]
    public void Docx_tenet_followed_by_empty_beat_at_chapter_end_legacy_leaves_a_blank_leaf_new_does_not()
    {
        // Reachable: a whitespace-only beat after a tenet. Legacy looks at the next BEAT (the empty
        // one), so it emits the closing page break and the next chapter emits another — the blank
        // leaf its own comment warns against. The Manuscript model has no empty beats, so the new
        // renderer sees the tenet as the chapter's last block and emits exactly one break.
        var book = new LegacyBook
        {
            Title = "T",
            Chapters =
            [
                new LegacyChapter("One", [Beat(1, "a"), Beat(2, "礼\nRei", kind: "tenet"), Beat(3, "   ")]),
                new LegacyChapter("Two", [Beat(4, "b")])
            ]
        };
        LegacyDocxExport.Export(book, Temp.File("legacy.docx"));
        var (m, o) = ProseAdapter.ForDocx(book);
        DocxRenderer.Render(m, Temp.File("new.docx"), o);
        var legacyBreaks = PageBreak.Matches(DocumentXml(Temp.File("legacy.docx"))).Count;
        var newBreaks = PageBreak.Matches(DocumentXml(Temp.File("new.docx"))).Count;
        Assert.That(legacyBreaks - newBreaks, Is.EqualTo(1));
    }

    [TestCase("one\n\u00A0\ntwo", 3, 2)]
    [TestCase("one\n\u3000\ntwo three", 4, 3)]
    [TestCase("one\n \u2003 \ntwo", 3, 2)]
    [TestCase("one\ntwo", 2, 2)]
    public void Docx_word_count_of_a_line_holding_only_non_ascii_whitespace(string text, int legacyWords, int newWords)
    {
        // Legacy counted the whole beat; the new renderer counts the paragraphs Prose splits it into
        // (TrimEntries drops a line of NBSP/ideographic space that CountWords saw as a word).
        var book = new LegacyBook { Title = "T", Chapters = [new LegacyChapter("One", [Beat(1, text)])] };
        var (lw, _) = LegacyDocxExport.Export(book, Temp.File("legacy.docx"));
        var (m, o) = ProseAdapter.ForDocx(book);
        var result = DocxRenderer.Render(m, Temp.File("new.docx"), o);
        Assert.That(lw, Is.EqualTo(legacyWords));
        Assert.That(result.WordCount, Is.EqualTo(newWords));
    }
}
