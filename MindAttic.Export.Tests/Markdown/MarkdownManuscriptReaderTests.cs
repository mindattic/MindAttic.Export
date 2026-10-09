using MindAttic.Export.Markdown;
using MindAttic.Export.Model;
using MindAttic.Export.Tests.Support;
using MindAttic.Export.Text;
using Paragraph = MindAttic.Export.Model.ParagraphBlock;

namespace MindAttic.Export.Tests.Markdown;

[TestFixture]
public class MarkdownManuscriptReaderTests : TempDirTestBase
{
    private static readonly MarkdownReaderOptions Default = new();

    private static List<Chapter> Read(string md, MarkdownReaderOptions? options = null) =>
        MarkdownManuscriptReader.ReadChapters(md, options ?? Default);

    private static string Plain(IEnumerable<ProseInline.Span> spans) => string.Concat(spans.Select(s => s.Text));

    // ── chapters ─────────────────────────────────────────────────────────────

    [Test]
    public void Each_level_one_heading_opens_a_chapter()
    {
        var chapters = Read("# One\n\nA.\n\n# Two\n\nB.\n\nC.\n");
        Assert.That(chapters.Select(c => c.Heading), Is.EqualTo(new[] { "One", "Two" }));
        Assert.That(chapters[0].Blocks, Has.Count.EqualTo(1));
        Assert.That(chapters[1].Blocks, Has.Count.EqualTo(2));
    }

    [Test]
    public void Text_before_the_first_heading_is_an_untitled_preamble_chapter()
    {
        var chapters = Read("Preface text.\n\nMore.\n\n# One\n\nA.\n");
        Assert.That(chapters.Select(c => c.Heading), Is.EqualTo(new[] { null, "One" }));
        Assert.That(chapters[0].Blocks.Cast<Paragraph>().Select(p => p.Text), Is.EqualTo(new[] { "Preface text.", "More." }));
    }

    [Test]
    public void No_headings_gives_one_untitled_chapter()
    {
        var chapters = Read("Just text.\n");
        Assert.That(chapters, Has.Count.EqualTo(1));
        Assert.That(chapters[0].Heading, Is.Null);
    }

    [Test]
    public void Empty_document_gives_no_chapters() => Assert.That(Read(""), Is.Empty);

    [Test]
    public void Heading_with_no_content_is_an_empty_chapter()
    {
        var chapters = Read("# One\n# Two\n");
        Assert.That(chapters.Select(c => c.Blocks.Count), Is.EqualTo(new[] { 0, 0 }));
    }

    [Test]
    public void Setext_heading_opens_a_chapter()
    {
        var chapters = Read("Title Here\n==========\n\nBody.\n");
        Assert.That(chapters.Single().Heading, Is.EqualTo("Title Here"));
    }

    [Test]
    public void Chapter_heading_inline_markup_is_flattened()
    {
        var chapters = Read("# The *Economic* **Layer** and `code`\n");
        Assert.That(chapters.Single().Heading, Is.EqualTo("The Economic Layer and code"));
    }

    [Test]
    public void Crlf_input_reads_like_lf()
    {
        var lf = Read("# One\n\nA line\ncontinued.\n\n- x\n- y\n");
        var crlf = Read("# One\r\n\r\nA line\r\ncontinued.\r\n\r\n- x\r\n- y\r\n");
        Assert.That(((Paragraph)crlf[0].Blocks[0]).Text, Is.EqualTo(((Paragraph)lf[0].Blocks[0]).Text));
        Assert.That(((Paragraph)crlf[0].Blocks[0]).Text, Is.EqualTo("A line continued."));
        Assert.That(crlf[0].Blocks, Has.Count.EqualTo(lf[0].Blocks.Count));
    }

    // ── heading normalisation ────────────────────────────────────────────────

    [TestCase("Chapter 4: The Economic Layer", "Chapter 4 — The Economic Layer")]
    [TestCase("Chapter 12:The Ledger", "Chapter 12 — The Ledger")]
    [TestCase("Chapter IV: Roman", "Chapter IV — Roman")]
    [TestCase("Chapter XLII : Spaced", "Chapter XLII — Spaced")]
    [TestCase("Appendix A: Proofs", "Appendix A — Proofs")]
    [TestCase("Appendix B:  Data  ", "Appendix B — Data")]
    [TestCase("Part 2: Markets", "Part 2 — Markets")]
    [TestCase("Part I: Origins", "Part I — Origins")]
    [TestCase("  Chapter 1: Padded  ", "Chapter 1 — Padded")]
    [TestCase("Chapter 1 — Already", "Chapter 1 — Already")]
    [TestCase("Chapter 1", "Chapter 1")]
    [TestCase("chapter 1: lower", "chapter 1: lower")]
    [TestCase("Chapter one: Word", "Chapter one: Word")]
    [TestCase("Appendix AB: Two letters", "Appendix AB: Two letters")]
    [TestCase("Introduction", "Introduction")]
    [TestCase("Section 3: Not a prefix", "Section 3: Not a prefix")]
    [TestCase("Chapter 3: A: B", "Chapter 3 — A: B")]
    public void NormalizeHeading_em_dash(string heading, string expected) =>
        Assert.That(MarkdownManuscriptReader.NormalizeHeading(heading, ChapterHeadingStyle.EmDash), Is.EqualTo(expected));

    [TestCase("Chapter 1:", "Chapter 1")]
    [TestCase("Appendix B: ", "Appendix B")]
    public void NormalizeHeading_with_empty_title_drops_the_colon(string heading, string expected) =>
        Assert.That(MarkdownManuscriptReader.NormalizeHeading(heading, ChapterHeadingStyle.EmDash), Is.EqualTo(expected));

    [TestCase("Chapter 4: The Economic Layer")]
    [TestCase("Appendix A: Proofs")]
    [TestCase("Anything")]
    public void NormalizeHeading_as_written_only_trims(string heading) =>
        Assert.That(MarkdownManuscriptReader.NormalizeHeading("  " + heading + " ", ChapterHeadingStyle.AsWritten), Is.EqualTo(heading));

    [Test]
    public void Reader_applies_heading_style()
    {
        Assert.That(Read("# Chapter 2: Two\n").Single().Heading, Is.EqualTo("Chapter 2 — Two"));
        Assert.That(Read("# Chapter 2: Two\n", Default with { HeadingStyle = ChapterHeadingStyle.AsWritten }).Single().Heading,
                    Is.EqualTo("Chapter 2: Two"));
    }

    // ── hanging-indent chapters ──────────────────────────────────────────────

    [TestCase("References")]
    [TestCase("Bibliography")]
    [TestCase("Works Cited")]
    [TestCase("references")]
    [TestCase("Appendix C: References")]
    [TestCase("Chapter 9: Bibliography")]
    public void Reference_chapters_get_hanging_paragraphs(string heading)
    {
        var chapters = Read($"# Body\n\nNormal.\n\n# {heading}\n\nSmith, J. (2020). *Title*.\n\nDoe, A. (2021). Other.\n");
        Assert.That(((Paragraph)chapters[0].Blocks[0]).Role, Is.EqualTo(ParagraphRole.Body));
        Assert.That(chapters[1].Blocks.Cast<Paragraph>().Select(p => p.Role), Is.All.EqualTo(ParagraphRole.Hanging));
    }

    [Test]
    public void Hanging_ends_at_the_next_chapter()
    {
        var chapters = Read("# References\n\nA.\n\n# Afterword\n\nB.\n");
        Assert.That(((Paragraph)chapters[1].Blocks[0]).Role, Is.EqualTo(ParagraphRole.Body));
    }

    [Test]
    public void Custom_hanging_chapter_list()
    {
        var options = Default with { HangingIndentChapters = ["Sources"] };
        var chapters = Read("# Sources\n\nA.\n\n# References\n\nB.\n", options);
        Assert.That(((Paragraph)chapters[0].Blocks[0]).Role, Is.EqualTo(ParagraphRole.Hanging));
        Assert.That(((Paragraph)chapters[1].Blocks[0]).Role, Is.EqualTo(ParagraphRole.Body));
    }

    // ── headings inside chapters ─────────────────────────────────────────────

    [TestCase("## Two", 2, "Two")]
    [TestCase("### Three", 3, "Three")]
    [TestCase("#### Four", 4, "Four")]
    [TestCase("##### Five", 5, "Five")]
    [TestCase("###### Six", 6, "Six")]
    [TestCase("## With *emphasis*", 2, "With *emphasis*")]
    public void Sub_headings_become_heading_blocks(string line, int level, string raw)
    {
        var block = (HeadingBlock)Read($"# C\n\n{line}\n").Single().Blocks.Single();
        Assert.That(block.Level, Is.EqualTo(level));
        Assert.That(block.Text, Is.EqualTo(raw));
    }

    [Test]
    public void Heading_spans_carry_styles()
    {
        var block = (HeadingBlock)Read("# C\n\n## A **bold** move\n").Single().Blocks.Single();
        Assert.That(block.Runs.Select(r => (r.Text, r.Style)), Is.EqualTo(new[]
        {
            ("A ", ProseInline.Style.None), ("bold", ProseInline.Style.Bold), (" move", ProseInline.Style.None)
        }));
    }

    // ── paragraphs and inline ────────────────────────────────────────────────

    [Test]
    public void Paragraph_keeps_its_markdown_source_on_one_line()
    {
        var p = (Paragraph)Read("Some *soft*\nand **hard** `code` text.\n").Single().Blocks.Single();
        Assert.That(p.Text, Is.EqualTo("Some *soft* and **hard** `code` text."));
        Assert.That(p.Spans, Is.Not.Null);
        Assert.That(p.Runs.Select(r => (r.Text, r.Style)), Is.EqualTo(new[]
        {
            ("Some ", ProseInline.Style.None), ("soft", ProseInline.Style.Italic), (" and ", ProseInline.Style.None),
            ("hard", ProseInline.Style.Bold), (" ", ProseInline.Style.None), ("code", ProseInline.Style.Code),
            (" text.", ProseInline.Style.None)
        }));
    }

    [TestCase("~~gone~~", "gone", ProseInline.Style.Strikethrough)]
    [TestCase("_under_", "under", ProseInline.Style.Italic)]
    [TestCase("__strong__", "strong", ProseInline.Style.Bold)]
    [TestCase("***both***", "both", ProseInline.Style.Bold | ProseInline.Style.Italic)]
    [TestCase("`a*b`", "a*b", ProseInline.Style.Code)]
    public void Inline_styles(string md, string text, ProseInline.Style style)
    {
        var p = (Paragraph)Read($"x {md} y\n").Single().Blocks.Single();
        Assert.That(p.Runs.Any(r => r.Text == text && r.Style == style), Is.True, string.Join(" | ", p.Runs));
    }

    [Test]
    public void Links_keep_their_text_and_images_vanish()
    {
        var p = (Paragraph)Read("See [the docs](http://x.y) and ![alt](a.png) and <http://auto.link>.\n").Single().Blocks.Single();
        Assert.That(Plain(p.Runs), Is.EqualTo("See the docs and  and http://auto.link."));
    }

    [Test]
    public void Hard_line_break_becomes_a_space()
    {
        var p = (Paragraph)Read("one  \ntwo\n").Single().Blocks.Single();
        Assert.That(Plain(p.Runs), Is.EqualTo("one two"));
    }

    [Test]
    public void Html_entities_are_decoded_and_inline_html_dropped()
    {
        var p = (Paragraph)Read("a &amp; b &mdash; <span>c</span>\n").Single().Blocks.Single();
        Assert.That(Plain(p.Runs), Is.EqualTo("a & b — c"));
    }

    [Test]
    public void Html_blocks_and_link_definitions_are_skipped()
    {
        var chapters = Read("<div>\nraw\n</div>\n\n[ref]: http://x\n\nText.\n");
        Assert.That(chapters.Single().Blocks, Has.Count.EqualTo(1));
    }

    // ── lists ────────────────────────────────────────────────────────────────

    [Test]
    public void Unordered_list()
    {
        var list = (ListBlock)Read("- one\n- *two*\n- three\n").Single().Blocks.Single();
        Assert.That(list.Ordered, Is.False);
        Assert.That(list.Start, Is.EqualTo(1));
        Assert.That(list.Items.Select(i => i.Text), Is.EqualTo(new[] { "one", "*two*", "three" }));
        Assert.That(list.Items[1].Runs.Single().Style, Is.EqualTo(ProseInline.Style.Italic));
    }

    [TestCase("1. a\n2. b\n", 1)]
    [TestCase("3. a\n4. b\n", 3)]
    [TestCase("0. a\n1. b\n", 0)]
    [TestCase("10) a\n11) b\n", 10)]
    public void Ordered_list_start(string md, int start)
    {
        var list = (ListBlock)Read(md).Single().Blocks.Single();
        Assert.That(list.Ordered, Is.True);
        Assert.That(list.Start, Is.EqualTo(start));
        Assert.That(list.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void Loose_list_item_with_two_paragraphs_joins_them()
    {
        var list = (ListBlock)Read("- first para\n\n  second para\n- next\n").Single().Blocks.Single();
        Assert.That(list.Items[0].Text, Is.EqualTo("first para second para"));
        Assert.That(Plain(list.Items[0].Runs), Is.EqualTo("first para second para"));
    }

    // ── quotes, code, rules, tables ──────────────────────────────────────────

    [Test]
    public void Block_quote_holds_its_blocks()
    {
        var quote = (QuoteBlock)Read("> Quoted *text*.\n>\n> - item\n").Single().Blocks.Single();
        Assert.That(quote.Blocks, Has.Count.EqualTo(2));
        Assert.That(((Paragraph)quote.Blocks[0]).Text, Is.EqualTo("Quoted *text*."));
        Assert.That(quote.Blocks[1], Is.TypeOf<ListBlock>());
    }

    [Test]
    public void Nested_block_quote()
    {
        var quote = (QuoteBlock)Read("> outer\n>\n> > inner\n").Single().Blocks.Single();
        Assert.That(quote.Blocks[1], Is.TypeOf<QuoteBlock>());
    }

    [TestCase("```\nline 1\n  line 2\n```\n", "line 1\n  line 2")]
    [TestCase("```csharp\nvar x = 1;\n```\n", "var x = 1;")]
    [TestCase("    indented\n    code\n", "indented\ncode")]
    [TestCase("~~~\n*not italic*\n~~~\n", "*not italic*")]
    public void Code_blocks_are_preformatted(string md, string text)
    {
        var p = (Paragraph)Read(md).Single().Blocks.Single();
        Assert.That(p.Role, Is.EqualTo(ParagraphRole.Preformatted));
        Assert.That(p.Text, Is.EqualTo(text));
        Assert.That(p.Runs.Single(), Is.EqualTo(new ProseInline.Span(text, ProseInline.Style.Code)));
    }

    [TestCase("---")]
    [TestCase("***")]
    [TestCase("___")]
    [TestCase("- - -")]
    public void Thematic_break_is_a_rule(string md)
    {
        var blocks = Read($"Before.\n\n{md}\n\nAfter.\n").Single().Blocks;
        Assert.That(blocks.Select(b => b.GetType()), Is.EqualTo(new[] { typeof(Paragraph), typeof(RuleBlock), typeof(Paragraph) }));
    }

    [Test]
    public void Pipe_table_rows_and_header()
    {
        var table = (TableBlock)Read("| A | **B** |\n|---|---|\n| 1 | 2 |\n| 3 |  |\n").Single().Blocks.Single();
        Assert.That(table.Rows.Select(r => r.IsHeader), Is.EqualTo(new[] { true, false, false }));
        Assert.That(table.Rows[0].Cells.Select(c => c.Text), Is.EqualTo(new[] { "A", "**B**" }));
        Assert.That(table.Rows[0].Cells[1].Runs.Single().Style, Is.EqualTo(ProseInline.Style.Bold));
        Assert.That(table.Rows[1].Cells.Select(c => c.Text), Is.EqualTo(new[] { "1", "2" }));
        Assert.That(table.Rows[2].Cells.Select(c => c.Text), Is.EqualTo(new[] { "3", "" }));
    }

    [Test]
    public void Escaped_pipe_in_table_cell()
    {
        var table = (TableBlock)Read("| A |\n|---|\n| a \\| b |\n").Single().Blocks.Single();
        Assert.That(Plain(table.Rows[1].Cells[0].Runs), Is.EqualTo("a | b"));
    }

    // ── math ─────────────────────────────────────────────────────────────────

    [Test]
    public void Inline_math_becomes_styled_spans_inside_the_paragraph()
    {
        var p = (Paragraph)Read("Energy $E = mc^2$ holds.\n").Single().Blocks.Single();
        Assert.That(p.Text, Is.EqualTo("Energy $E = mc^2$ holds."));
        Assert.That(Plain(p.Runs), Is.EqualTo("Energy E = mc2 holds."));
        Assert.That(p.Runs.Any(r => r.Text == "2" && r.Style == ProseInline.Style.Superscript), Is.True);
    }

    [Test]
    public void Single_line_display_math_paragraph_is_a_math_block()
    {
        var m = (MathBlock)Read("$$x^2 + y^2 = z^2$$\n").Single().Blocks.Single();
        Assert.That(m.Latex, Is.EqualTo("x^2 + y^2 = z^2"));
        Assert.That(Plain(m.Spans), Is.EqualTo(LatexMath.ToPlainText("x^2 + y^2 = z^2")));
    }

    [Test]
    public void Multi_line_display_math_is_a_math_block()
    {
        var m = (MathBlock)Read("$$\n\\frac{a}{b}\n$$\n").Single().Blocks.Single();
        Assert.That(m.Latex.Trim(), Is.EqualTo("\\frac{a}{b}"));
        Assert.That(Plain(m.Spans), Is.EqualTo("a/b"));
    }

    [Test]
    public void Display_math_with_surrounding_text_stays_a_paragraph()
    {
        var p = Read("Here $$x$$ there.\n").Single().Blocks.Single();
        Assert.That(p, Is.TypeOf<Paragraph>());
    }

    [Test]
    public void Math_disabled_keeps_dollars_literal()
    {
        var options = Default with { Math = false };
        var p = (Paragraph)Read("Costs $5 and $10 today.\n", options).Single().Blocks.Single();
        Assert.That(Plain(p.Runs), Is.EqualTo("Costs $5 and $10 today."));
        var q = (Paragraph)Read("$$x^2$$\n", options).Single().Blocks.Single();
        Assert.That(Plain(q.Runs), Is.EqualTo("$$x^2$$"));
    }

    // ── ManuscriptInfo, Read, ReadFiles, ReadReport ──────────────────────────

    [Test]
    public void Read_fills_manuscript_metadata()
    {
        var info = new ManuscriptInfo { Title = "T", Subtitle = "S", Author = "A", Description = "D", Slug = "s", Keywords = ["k1", "k2"] };
        var m = MarkdownManuscriptReader.Read("# One\n\nx\n", info);
        Assert.That((m.Title, m.Subtitle, m.Author, m.Description, m.Slug), Is.EqualTo(("T", "S", "A", "D", "s")));
        Assert.That(m.Keywords, Is.EqualTo(new[] { "k1", "k2" }));
        Assert.That(m.Glossary, Is.Empty);
        Assert.That(m.Chapters.Single().Heading, Is.EqualTo("One"));
    }

    [Test]
    public void ReadFiles_concatenates_in_order()
    {
        File.WriteAllText(Temp.File("b.md"), "# Two\n\nB.\n");
        File.WriteAllText(Temp.File("a.md"), "Pre.\n\n# One\n\nA.\n");
        var m = MarkdownManuscriptReader.ReadFiles([Temp.File("a.md"), Temp.File("b.md")], new ManuscriptInfo { Title = "T" });
        Assert.That(m.Chapters.Select(c => c.Heading), Is.EqualTo(new[] { null, "One", "Two" }));
    }

    [Test]
    public void ReadReport_takes_the_first_heading_as_title()
    {
        var m = MarkdownManuscriptReader.ReadReport("# Report Title\n\nIntro.\n\n# Section A\n\nText.\n\n## Sub\n\nMore.\n", "Fallback", "Sub", "Me");
        Assert.That(m.Title, Is.EqualTo("Report Title"));
        Assert.That((m.Subtitle, m.Author), Is.EqualTo(("Sub", "Me")));
        var chapter = m.Chapters.Single();
        Assert.That(chapter.Heading, Is.Null);
        Assert.That(chapter.Blocks.OfType<HeadingBlock>().Select(h => (h.Level, h.Text)), Is.EqualTo(new[] { (2, "Section A"), (2, "Sub") }));
    }

    [Test]
    public void ReadReport_without_heading_uses_fallback_title()
    {
        var m = MarkdownManuscriptReader.ReadReport("Just text.\n", "Fallback");
        Assert.That(m.Title, Is.EqualTo("Fallback"));
        Assert.That(m.Chapters.Single().Blocks, Has.Count.EqualTo(1));
    }

    // ── robustness over generated documents ──────────────────────────────────

    public static IEnumerable<TestCaseData> DocumentSeeds() =>
        Enumerable.Range(0, 120).Select(s => new TestCaseData(s).SetName($"Generated_document_reads_every_chapter_heading(seed {s})"));

    [TestCaseSource(nameof(DocumentSeeds))]
    public void Generated_document_reads_every_chapter_heading(int seed)
    {
        var doc = DocumentGen.Generate(seed);
        var chapters = Read(doc.Markdown);
        Assert.That(chapters.Where(c => c.Heading is not null).Select(c => c.Heading), Is.EqualTo(doc.ExpectedHeadings));
        Assert.That(chapters.First().Heading is null, Is.EqualTo(doc.HasPreamble));
        Assert.That(chapters.SelectMany(c => c.Blocks).OfType<TableBlock>().Count(), Is.EqualTo(doc.Tables));
        Assert.That(chapters.SelectMany(c => c.Blocks).OfType<MathBlock>().Count(), Is.EqualTo(doc.DisplayMath));
    }
}
