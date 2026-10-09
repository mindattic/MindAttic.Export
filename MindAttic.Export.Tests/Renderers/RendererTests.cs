using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using MindAttic.Export.Model;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Support;
using MindAttic.Export.Text;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace MindAttic.Export.Tests.Renderers;

[TestFixture]
public class RendererTests : TempDirTestBase
{
    private static readonly string NL = Environment.NewLine;

    private static Manuscript One(params Block[] blocks) => new() { Title = "T", Chapters = [new Chapter("C", [.. blocks])] };

    private static ProseInline.Span S(string text, ProseInline.Style style = ProseInline.Style.None) => new(text, style);

    // ── TXT ──────────────────────────────────────────────────────────────────

    [Test]
    public void Txt_title_block_and_chapters()
    {
        var m = new Manuscript
        {
            Title = "Title",
            Subtitle = "  Sub  ",
            Author = " Ryan ",
            Chapters = [new Chapter(null, [new ParagraphBlock("pre")]), new Chapter("One", [new ParagraphBlock("*a* b")])],
            Glossary = [new GlossaryEntry("G", null, "never printed")]
        };
        Assert.That(TextRenderer.Render(m), Is.EqualTo($"Title{NL}Sub{NL}by Ryan{NL}{NL}pre{NL}{NL}One{NL}{NL}a b\n"));
    }

    [TestCase("2", ProseInline.Style.Superscript, "²")]
    [TestCase("10", ProseInline.Style.Superscript, "¹⁰")]
    [TestCase("n+1", ProseInline.Style.Superscript, "ⁿ⁺¹")]
    [TestCase("−1", ProseInline.Style.Superscript, "⁻¹")]
    [TestCase("x", ProseInline.Style.Superscript, "^x")]
    [TestCase("ab", ProseInline.Style.Superscript, "^(ab)")]
    [TestCase("i", ProseInline.Style.Subscript, "ᵢ")]
    [TestCase("max", ProseInline.Style.Subscript, "ₘₐₓ")]
    [TestCase("ij", ProseInline.Style.Subscript, "ᵢⱼ")]
    [TestCase("b", ProseInline.Style.Subscript, "_b")]
    [TestCase("bc", ProseInline.Style.Subscript, "_(bc)")]
    [TestCase("0123456789", ProseInline.Style.Subscript, "₀₁₂₃₄₅₆₇₈₉")]
    [TestCase("plain", ProseInline.Style.Italic, "plain")]
    public void Txt_scripts_use_unicode_when_every_char_maps(string text, ProseInline.Style style, string expected)
    {
        var txt = TextRenderer.Render(One(new MathBlock("ignored", [S("x", ProseInline.Style.Italic), S(text, style)])));
        Assert.That(txt, Does.Contain("x" + expected));
    }

    [Test]
    public void Txt_document_blocks()
    {
        var m = One(
            new HeadingBlock(2, "Heading *two*"),
            new ListBlock(true, 3, [new ParagraphBlock("a", [S("a")]), new ParagraphBlock("b", [S("b", ProseInline.Style.Bold)])]),
            new ListBlock(false, 1, [new ParagraphBlock("c")]),
            new QuoteBlock([new ParagraphBlock("quoted"), new QuoteBlock([new ParagraphBlock("deeper")])]),
            new TableBlock([new Model.TableRow(true, [new ParagraphBlock("h1"), new ParagraphBlock("h2")]), new Model.TableRow(false, [new ParagraphBlock("c1")])]),
            new RuleBlock(),
            new MarkerBlock("beat:1:x"),
            new TenetBlock(" 礼 \n\n Rei "),
            new SubHeadingBlock("**Sub**"));
        var expected = string.Join(NL, "T", "", "C", "", "Heading two", "", "3. a", "4. b", "", "- c", "", "    quoted", "",
            "        deeper", "", "h1 | h2", "c1", "", "* * *", "", "礼", "", "Rei", "", "Sub") + "\n";
        Assert.That(TextRenderer.Render(m), Is.EqualTo(expected));
    }

    [Test]
    public async Task Txt_file_is_utf8_without_bom()
    {
        var path = Temp.File("a.txt");
        var result = await new TextRenderer().RenderAsync(One(new ParagraphBlock("é")), path, ExportOptions.ProseBook);
        Assert.That(result, Is.EqualTo(new RenderResult(path)));
        Assert.That(File.ReadAllBytes(path).Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    // ── MD ───────────────────────────────────────────────────────────────────

    [Test]
    public void Md_document_blocks()
    {
        var m = One(
            new MarkerBlock("beat:1:abc"),
            new HeadingBlock(2, "H2"), new HeadingBlock(3, "H3"), new HeadingBlock(5, "H5"), new HeadingBlock(9, "H9"),
            new ParagraphBlock("code\r\nline", null, ParagraphRole.Preformatted),
            new ListBlock(true, 7, [new ParagraphBlock("x"), new ParagraphBlock("y")]),
            new QuoteBlock([new ParagraphBlock("q"), new ListBlock(false, 1, [new ParagraphBlock("li")])]),
            new TableBlock([new Model.TableRow(false, [new ParagraphBlock("a|b"), new ParagraphBlock("c")]), new Model.TableRow(true, [new ParagraphBlock("H"), new ParagraphBlock("I")])]),
            new TableBlock([]),
            new MathBlock("  x^2  ", []),
            new RuleBlock());
        var expected = string.Join(NL,
            "# T", "", "## C", "", "<!-- beat:1:abc -->", "### H2", "", "#### H3", "", "###### H5", "", "###### H9", "",
            "```", "code", "line", "```", "", "7. x", "8. y", "", "> q", ">", "> - li", ">",
            "| H | I |", "|---|---|", "| a\\|b | c |", "", "$$", "x^2", "$$", "", "---") + "\n";
        Assert.That(MarkdownRenderer.Render(m), Is.EqualTo(expected));
    }

    // ── EPUB ─────────────────────────────────────────────────────────────────

    [Test]
    public void Epub_renders_spans_with_every_style()
    {
        var html = EpubRenderer.RenderSpans([
            S("b", ProseInline.Style.Bold), S("i", ProseInline.Style.Italic), S("u", ProseInline.Style.Underline),
            S("s", ProseInline.Style.Strikethrough), S("2", ProseInline.Style.Superscript), S("0", ProseInline.Style.Subscript),
            S("c<d", ProseInline.Style.Code), S("all", ProseInline.Style.Bold | ProseInline.Style.Italic | ProseInline.Style.Underline)]);
        Assert.That(html, Is.EqualTo("<strong>b</strong><em>i</em><u>u</u><s>s</s><sup>2</sup><sub>0</sub><code>c&lt;d</code><strong><em><u>all</u></em></strong>"));
    }

    [TestCase("a & b", "a &amp; b")]
    [TestCase("<x>", "&lt;x&gt;")]
    [TestCase("\"q\"", "&quot;q&quot;")]
    [TestCase("it's", "it's")]
    [TestCase("\u0001\u0008\u000B\u000C\u000E\u001F", "")]
    [TestCase("\t\n\r", "\t\n\r")]
    [TestCase("&amp;", "&amp;amp;")]
    [TestCase(null, "")]
    public void Epub_escape(string? input, string expected) => Assert.That(EpubRenderer.Esc(input), Is.EqualTo(expected));

    [Test]
    public void Epub_language_defaults_to_en()
    {
        var path = Temp.File("en.epub");
        EpubRenderer.Render(One(new ParagraphBlock("x")), path, ExportOptions.ProseBook);
        var opf = Packages.ReadZip(path).Single(e => e.Name == "OEBPS/content.opf").Text;
        var title = Packages.ReadZip(path).Single(e => e.Name == "OEBPS/title.xhtml").Text;
        Assert.That(opf, Does.Contain("<dc:language>en</dc:language>"));
        Assert.That(opf, Does.Contain("xml:lang=\"en\""));
        Assert.That(title, Does.Contain("xml:lang=\"en\""));
    }

    [Test]
    public void Epub_honours_manuscript_language()
    {
        var m = new Manuscript { Title = "T", Language = "fr", Chapters = [new Chapter("C", [new ParagraphBlock("x")])] };
        var path = Temp.File("fr.epub");
        EpubRenderer.Render(m, path, ExportOptions.ProseBook);
        var entries = Packages.ReadZip(path);
        var opf = entries.Single(e => e.Name == "OEBPS/content.opf").Text;
        var title = entries.Single(e => e.Name == "OEBPS/title.xhtml").Text;
        var toc = entries.Single(e => e.Name == "OEBPS/toc.xhtml").Text;
        var chapter = entries.Single(e => e.Name == "OEBPS/chapter-001.xhtml").Text;
        Assert.That(opf, Does.Contain("<dc:language>fr</dc:language>"));
        Assert.That(opf, Does.Contain("xml:lang=\"fr\""));
        Assert.That(title, Does.Contain("xml:lang=\"fr\""));
        Assert.That(toc, Does.Contain("xml:lang=\"fr\""));
        Assert.That(chapter, Does.Contain("xml:lang=\"fr\""));
    }

    [Test]
    public void Epub_without_identifier_or_timestamp_mints_them()
    {
        var a = Temp.File("a.epub");
        EpubRenderer.Render(One(new ParagraphBlock("x")), a, ExportOptions.ProseBook);
        var opf = Packages.ReadZip(a).Single(e => e.Name == "OEBPS/content.opf").Text;
        Assert.That(opf, Does.Match(@"<dc:identifier id=""bookid"">urn:uuid:[0-9a-f-]{36}</dc:identifier>"));
        Assert.That(opf, Does.Match(@"dcterms:modified"">\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ<"));
    }

    [Test]
    public void Epub_document_blocks_markup()
    {
        var path = Temp.File("d.epub");
        EpubRenderer.Render(One(
            new HeadingBlock(2, "h", [S("h")]), new HeadingBlock(4, "h4"),
            new ListBlock(true, 1, [new ParagraphBlock("a")]), new ListBlock(true, 5, [new ParagraphBlock("b")]), new ListBlock(false, 1, [new ParagraphBlock("c")]),
            new QuoteBlock([new ParagraphBlock("q")]),
            new TableBlock([new Model.TableRow(true, [new ParagraphBlock("H")]), new Model.TableRow(false, [new ParagraphBlock("D")])]),
            new MathBlock("x", [S("x", ProseInline.Style.Italic)]), new RuleBlock(), new MarkerBlock("m"),
            new ParagraphBlock("a<b", null, ParagraphRole.Preformatted), new ParagraphBlock("ref", null, ParagraphRole.Hanging)),
            path, ExportOptions.Document);
        var ch = Packages.ReadZip(path).Single(e => e.Name == "OEBPS/chapter-001.xhtml").Text;
        foreach (var fragment in new[]
        {
            "<h3 class=\"doc-heading\">h</h3>", "<h5 class=\"doc-heading\">h4</h5>", "<ol>", "<ol start=\"5\">", "<ul>", "<li>c</li>",
            "<blockquote>", "<table class=\"doc-table\">", "<tr><th>H</th></tr>", "<tr><td>D</td></tr>", "<p class=\"math\"><em>x</em></p>",
            "<p class=\"rule\">* * *</p>", "<pre>a&lt;b</pre>", "<p class=\"hanging\">ref</p>"
        })
            Assert.That(ch, Does.Contain(fragment));
        Assert.That(ch, Does.Not.Contain("m</"));
        var css = Packages.ReadZip(path).Single(e => e.Name == "OEBPS/styles.css").Text;
        Assert.That(css, Does.Contain("table.doc-table"));
    }

    [TestCaseSource(nameof(DocumentBlockKinds))]
    public void Epub_stylesheet_gains_document_rules_only_for_document_blocks(Block block, bool documentCss)
    {
        var path = Temp.File("s.epub");
        EpubRenderer.Render(One(new ParagraphBlock("x"), block), path, ExportOptions.ProseBook);
        var css = Packages.ReadZip(path).Single(e => e.Name == "OEBPS/styles.css").Text;
        Assert.That(css.Contains("table.doc-table"), Is.EqualTo(documentCss));
    }

    public static IEnumerable<TestCaseData> DocumentBlockKinds()
    {
        yield return new TestCaseData(new ParagraphBlock("p"), false).SetName("Epub_css(ParagraphBlock body)");
        yield return new TestCaseData(new SubHeadingBlock("s"), false).SetName("Epub_css(SubHeadingBlock)");
        yield return new TestCaseData(new TenetBlock("t"), false).SetName("Epub_css(TenetBlock)");
        yield return new TestCaseData(new MarkerBlock("m"), false).SetName("Epub_css(MarkerBlock)");
        yield return new TestCaseData(new ParagraphBlock("p", [new ProseInline.Span("p", ProseInline.Style.None)]), true).SetName("Epub_css(ParagraphBlock with spans)");
        yield return new TestCaseData(new ParagraphBlock("p", null, ParagraphRole.Hanging), true).SetName("Epub_css(ParagraphBlock hanging)");
        yield return new TestCaseData(new ParagraphBlock("p", null, ParagraphRole.Preformatted), true).SetName("Epub_css(ParagraphBlock preformatted)");
        yield return new TestCaseData(new HeadingBlock(2, "h"), true).SetName("Epub_css(HeadingBlock)");
        yield return new TestCaseData(new ListBlock(false, 1, []), true).SetName("Epub_css(ListBlock)");
        yield return new TestCaseData(new QuoteBlock([]), true).SetName("Epub_css(QuoteBlock)");
        yield return new TestCaseData(new TableBlock([]), true).SetName("Epub_css(TableBlock)");
        yield return new TestCaseData(new MathBlock("x", []), true).SetName("Epub_css(MathBlock)");
        yield return new TestCaseData(new RuleBlock(), true).SetName("Epub_css(RuleBlock)");
    }

    // ── DOCX ─────────────────────────────────────────────────────────────────

    private static string BodyXml(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        return doc.MainDocumentPart!.Document!.Body!.OuterXml;
    }

    [TestCase(0, 540u)]
    [TestCase(150, 540u)]
    [TestCase(151, 720u)]
    [TestCase(400, 720u)]
    [TestCase(401, 900u)]
    [TestCase(600, 900u)]
    [TestCase(601, 1080u)]
    [TestCase(700, 1080u)]
    [TestCase(701, 1260u)]
    public void Docx_gutter_follows_the_KDP_table(int pages, uint gutter)
    {
        // estimatedPages = round(words / 306 + chapters * 1.1); one chapter → words ≈ (pages - 1.1) * 306.
        var words = Math.Max(0, (int)Math.Round((pages - 1.1) * 306));
        var m = One(new ParagraphBlock(string.Join(' ', Enumerable.Repeat("w", words))));
        var path = Temp.File("g.docx");
        var result = DocxRenderer.Render(m, path, ExportOptions.ProseBook with { ChapterCount = 1 });
        Assert.That(result.EstimatedPages, Is.EqualTo(Math.Max(1, pages)));
        using var doc = WordprocessingDocument.Open(path, false);
        var margin = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageMargin>().Single();
        Assert.That((uint)margin.Gutter!, Is.EqualTo(gutter));
    }

    [Test]
    public void Docx_letter_profile_has_no_gutter_or_mirror_margins()
    {
        var path = Temp.File("l.docx");
        DocxRenderer.Render(One(new ParagraphBlock("x")), path, ExportOptions.Document);
        using var doc = WordprocessingDocument.Open(path, false);
        var margin = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageMargin>().Single();
        Assert.That(((uint)margin.Gutter!, (uint)margin.Left!, (int)margin.Top!), Is.EqualTo((0u, 1440u, 1440)));
        Assert.That(doc.MainDocumentPart.DocumentSettingsPart!.Settings!.Elements<W.MirrorMargins>(), Is.Empty);
        Assert.That(doc.MainDocumentPart.FooterParts.Single().Footer!.InnerText, Is.EqualTo(" PAGE 1"));
    }

    [TestCase(null, 1, false)]
    [TestCase(1, 1, false)]
    [TestCase(2, 2, true)]
    [TestCase(null, 2, true)]
    public void Docx_chapter_headings_only_print_for_two_or_more_chapters(int? chapterCountOption, int chapters, bool printed)
    {
        var m = new Manuscript { Title = "T", Chapters = [.. Enumerable.Range(1, chapters).Select(i => new Chapter($"Heading{i}", [new ParagraphBlock("x")]))] };
        var path = Temp.File("h.docx");
        DocxRenderer.Render(m, path, ExportOptions.ProseBook with { ChapterCount = chapterCountOption, IncludeToc = true });
        var xml = BodyXml(path);
        Assert.That(xml.Contains("Heading1\""), Is.EqualTo(printed));
        Assert.That(xml.Contains("Table of Contents"), Is.EqualTo(printed));
    }

    [Test]
    public void Docx_font_family_option_is_used_everywhere()
    {
        var path = Temp.File("f.docx");
        DocxRenderer.Render(One(new ParagraphBlock("x")), path, ExportOptions.ProseBook with { FontFamily = "Georgia" });
        using var doc = WordprocessingDocument.Open(path, false);
        var fonts = doc.MainDocumentPart!.Document!.Body!.Descendants<W.RunFonts>().Select(f => f.Ascii!.Value).Distinct();
        Assert.That(fonts, Is.EqualTo(new[] { "Georgia" }));
        Assert.That(doc.MainDocumentPart.StyleDefinitionsPart!.Styles!.OuterXml, Does.Contain("Georgia").And.Not.Contain("Garamond"));
    }

    [Test]
    public void Docx_word_count_covers_document_blocks()
    {
        var m = One(
            new ParagraphBlock("one two"),
            new ListBlock(false, 1, [new ParagraphBlock("three"), new ParagraphBlock("four five")]),
            new QuoteBlock([new ParagraphBlock("six"), new ListBlock(true, 1, [new ParagraphBlock("seven")])]),
            new TableBlock([new Model.TableRow(true, [new ParagraphBlock("eight"), new ParagraphBlock("nine")]), new Model.TableRow(false, [new ParagraphBlock("ten")])]),
            new HeadingBlock(2, "not counted by docx"),
            new SubHeadingBlock("not counted"),
            new TenetBlock("not counted"),
            new MathBlock("x", [S("x")]));
        var result = DocxRenderer.Render(m, Temp.File("w.docx"), ExportOptions.Document);
        Assert.That(result.WordCount, Is.EqualTo(10));
    }

    [Test]
    public void Docx_scrubs_xml_illegal_controls_from_text()
    {
        var path = Temp.File("c.docx");
        var m = new Manuscript
        {
            Title = "Ti\u0007tle",
            Subtitle = "S\u000Bub",
            Chapters = [new Chapter("He\u0001ad", [new ParagraphBlock("bo\u001Fdy")]), new Chapter("Two", [new SubHeadingBlock("su\u000Cb")])],
            Glossary = [new GlossaryEntry("Te\u0002rm", "Fu\u0003ll", "De\u0004f")]
        };
        DocxRenderer.Render(m, path, ExportOptions.ProseBook with { IncludeToc = true });
        var xml = BodyXml(path);
        foreach (var word in new[] { "Title", "Sub", "Head", "body", "sub", "Term", "Full", "Def" })
            Assert.That(xml, Does.Contain(word));
        Assert.That(Regex.IsMatch(xml, @"[\x00-\x08\x0B\x0C\x0E-\x1F]"), Is.False);
    }

    [TestCase("Ry\u0007an")]
    [TestCase("\u001BEsc")]
    public void Docx_author_with_control_character_does_not_throw(string author)
    {
        var m = new Manuscript { Title = "T", Author = author, Chapters = [new Chapter(null, [new ParagraphBlock("x")])] };
        Assert.DoesNotThrow(() => DocxRenderer.Render(m, Temp.File("a.docx"), ExportOptions.ProseBook));
    }

    [Test]
    public void Docx_superscript_and_code_spans()
    {
        var path = Temp.File("s.docx");
        DocxRenderer.Render(One(new ParagraphBlock("x", [S("x", ProseInline.Style.Italic), S("2", ProseInline.Style.Superscript), S("i", ProseInline.Style.Subscript), S("c", ProseInline.Style.Code)])), path, ExportOptions.Document);
        var xml = BodyXml(path);
        Assert.That(xml, Does.Contain("w:vertAlign w:val=\"superscript\""));
        Assert.That(xml, Does.Contain("w:vertAlign w:val=\"subscript\""));
        Assert.That(xml, Does.Contain("w:ascii=\"Consolas\""));
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    [Test]
    public void Pdf_fixed_timestamp_makes_output_reproducible()
    {
        var options = ExportOptions.Document with { FixedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var m = One(new ParagraphBlock("Hello *world*"), new TableBlock([new Model.TableRow(true, [new ParagraphBlock("a")])]));
        PdfRenderer.Render(m, Temp.File("a.pdf"), options);
        PdfRenderer.Render(m, Temp.File("b.pdf"), options);
        var a = File.ReadAllBytes(Temp.File("a.pdf"));
        var b = File.ReadAllBytes(Temp.File("b.pdf"));
        Assert.That(Support.Pdf.Normalised(a), Is.EqualTo(Support.Pdf.Normalised(b)));
        Assert.That(Support.Pdf.Latin1(a), Does.Contain("D:20260101"));
    }

    [Test]
    public async Task Pdf_render_async_returns_the_path()
    {
        var path = Temp.File("r.pdf");
        var result = await new PdfRenderer().RenderAsync(One(new ParagraphBlock("x")), path, ExportOptions.ProseBook);
        Assert.That(result, Is.EqualTo(new RenderResult(path)));
        Assert.That(Support.Pdf.LooksValid(File.ReadAllBytes(path)), Is.True);
    }

    [Test]
    public void Renderer_formats()
    {
        Assert.That(new DocxRenderer().Format, Is.EqualTo(ExportFormat.Docx));
        Assert.That(new EpubRenderer().Format, Is.EqualTo(ExportFormat.Epub));
        Assert.That(new PdfRenderer().Format, Is.EqualTo(ExportFormat.Pdf));
        Assert.That(new TextRenderer().Format, Is.EqualTo(ExportFormat.Txt));
        Assert.That(new MarkdownRenderer().Format, Is.EqualTo(ExportFormat.Md));
    }

    // ── ReportExporter ───────────────────────────────────────────────────────

    [Test]
    public async Task ReportExporter_writes_markdown_verbatim_and_renders_the_rest()
    {
        const string md = "# Book Report: GLMZ\n\nIntro *text*.\n\n# Findings\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";
        var files = await ReportExporter.ExportAsync(md, Temp.Path, "GLMZ_BookReport",
            [ExportFormat.Md, ExportFormat.Txt, ExportFormat.Docx, ExportFormat.Pdf, ExportFormat.Epub, ExportFormat.Md], "Fallback");
        Assert.That(files.Keys, Is.EquivalentTo(Enum.GetValues<ExportFormat>()));
        Assert.That(File.ReadAllText(files[ExportFormat.Md]), Is.EqualTo(md));
        var txt = File.ReadAllText(files[ExportFormat.Txt]);
        Assert.That(txt, Does.StartWith("Book Report: GLMZ"));
        Assert.That(txt, Does.Contain("Findings"));
        Structural.StructuralValidityTests.AssertOnlyKnownDefects(Structural.StructuralValidityTests.Validate(files[ExportFormat.Docx]));
        Assert.That(Support.Pdf.LooksValid(File.ReadAllBytes(files[ExportFormat.Pdf])), Is.True);
        Assert.That(Packages.FirstLocalEntry(files[ExportFormat.Epub]).Name, Is.EqualTo("mimetype"));
        Assert.That(Directory.GetFiles(Temp.Path, "*.tmp"), Is.Empty);
    }

    [Test]
    public async Task ReportExporter_archives_a_previous_report()
    {
        await ReportExporter.ExportAsync("# A\n\nx\n", Temp.Path, "R", [ExportFormat.Md], "F");
        await ReportExporter.ExportAsync("# B\n\ny\n", Temp.Path, "R", [ExportFormat.Md], "F");
        Assert.That(File.ReadAllText(Temp.File("R.md")), Is.EqualTo("# B\n\ny\n"));
        Assert.That(Directory.GetFiles(Temp.Sub("Archives")), Has.Length.EqualTo(1));
    }
}
