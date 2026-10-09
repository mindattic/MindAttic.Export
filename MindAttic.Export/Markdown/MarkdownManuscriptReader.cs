using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MindAttic.Export.Model;
using MindAttic.Export.Text;
using MdParagraph = Markdig.Syntax.ParagraphBlock;
using MdHeading = Markdig.Syntax.HeadingBlock;
using MdList = Markdig.Syntax.ListBlock;
using MdQuote = Markdig.Syntax.QuoteBlock;
using MdMath = Markdig.Extensions.Mathematics.MathBlock;
using Paragraph = MindAttic.Export.Model.ParagraphBlock;
using Block = MindAttic.Export.Model.Block;

namespace MindAttic.Export.Markdown;

/// <summary>How chapter headings are normalised when read.</summary>
public enum ChapterHeadingStyle
{
    /// <summary>Headings are used exactly as written.</summary>
    AsWritten,

    /// <summary>Prose house style: "Chapter 4: The Economic Layer" becomes
    /// "Chapter 4 — The Economic Layer" (and likewise for "Appendix A: …").</summary>
    EmDash
}

public sealed record MarkdownReaderOptions
{
    public ChapterHeadingStyle HeadingStyle { get; init; } = ChapterHeadingStyle.EmDash;

    /// <summary>Chapters whose paragraphs are reference-list entries (hanging indent).</summary>
    public IReadOnlyCollection<string> HangingIndentChapters { get; init; } = ["References", "Bibliography", "Works Cited"];

    /// <summary>Parse <c>$…$</c> and <c>$$…$$</c> as LaTeX math. Turn off for documents that use
    /// dollar signs as currency.</summary>
    public bool Math { get; init; } = true;

    /// <summary>Report mode: the first level-1 heading is the document's title (it becomes the
    /// title page, not a chapter) and the rest of the document is one untitled chapter whose
    /// level-1 and level-2 sections become headings. For single-document reports such as Prose
    /// book reports.</summary>
    public bool FirstHeadingIsTitle { get; init; }
}

/// <summary>
/// Reads Markdown documents (Prose book reports, the SET dissertation, any report) into a
/// <see cref="Manuscript"/>. Every level-1 heading opens a new chapter (on a new page in docx and
/// pdf); text before the first one forms an untitled opening chapter. Level 2–4 headings become
/// <see cref="HeadingBlock"/>s. Paragraph source text is kept so the Markdown renderer can write
/// it back unchanged.
/// </summary>
public static class MarkdownManuscriptReader
{
    private static readonly Regex ChapterPrefix = new(@"^(Chapter|Appendix|Part)\s+([0-9IVXLC]+|[A-Z])\s*:\s*", RegexOptions.Compiled);

    public static MarkdownPipeline Pipeline(bool math)
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough);
        if (math) builder = builder.UseMathematics();
        return builder.Build();
    }

    /// <summary>Reads files in order into one manuscript.</summary>
    public static Manuscript ReadFiles(IEnumerable<string> paths, ManuscriptInfo info, MarkdownReaderOptions? options = null)
    {
        options ??= new MarkdownReaderOptions();
        var chapters = new List<Chapter>();
        foreach (var path in paths)
            chapters.AddRange(ReadChapters(File.ReadAllText(path), options));
        return info.ToManuscript(chapters);
    }

    public static Manuscript Read(string markdown, ManuscriptInfo info, MarkdownReaderOptions? options = null) =>
        info.ToManuscript(ReadChapters(markdown, options ?? new MarkdownReaderOptions()));

    /// <summary>
    /// Reads a single-document report (see <see cref="MarkdownReaderOptions.FirstHeadingIsTitle"/>).
    /// The title is the first level-1 heading, or <paramref name="fallbackTitle"/> when there is none.
    /// </summary>
    public static Manuscript ReadReport(string markdown, string fallbackTitle, string? subtitle = null,
                                        string? author = null, MarkdownReaderOptions? options = null)
    {
        options = (options ?? new MarkdownReaderOptions()) with { FirstHeadingIsTitle = true };
        var (chapters, title) = ReadCore(markdown, options);
        return new Manuscript { Title = title ?? fallbackTitle, Subtitle = subtitle, Author = author, Chapters = chapters };
    }

    public static List<Chapter> ReadChapters(string markdown, MarkdownReaderOptions options) =>
        ReadCore(markdown, options).Chapters;

    private static (List<Chapter> Chapters, string? Title) ReadCore(string markdown, MarkdownReaderOptions options)
    {
        string? documentTitle = null;
        var source = markdown.Replace("\r\n", "\n");
        var doc = Markdig.Markdown.Parse(source, Pipeline(options.Math));
        var chapters = new List<Chapter>();
        Chapter? current = null;
        var hanging = false;

        foreach (var block in doc)
        {
            if (options.FirstHeadingIsTitle)
            {
                if (block is MdHeading { Level: 1 } titleHeading && documentTitle is null)
                {
                    documentTitle = InlineText(titleHeading.Inline);
                    continue;
                }
                if (current is null)
                {
                    current = new Chapter((string?)null);
                    chapters.Add(current);
                }
                if (block is MdHeading { Level: 1 } section)
                {
                    current.Blocks.Add(new Model.HeadingBlock(2, Raw(source, section.Inline), Spans(section.Inline)));
                    continue;
                }
                foreach (var converted in Convert(block, source, hanging)) current.Blocks.Add(converted);
                continue;
            }
            if (block is MdHeading { Level: 1 } h1)
            {
                var heading = NormalizeHeading(InlineText(h1.Inline), options.HeadingStyle);
                current = new Chapter(heading);
                chapters.Add(current);
                hanging = options.HangingIndentChapters.Any(c =>
                    StripChapterPrefix(heading).Equals(c, StringComparison.OrdinalIgnoreCase));
                continue;
            }
            if (current is null)
            {
                current = new Chapter((string?)null);
                chapters.Add(current);
            }
            foreach (var converted in Convert(block, source, hanging)) current.Blocks.Add(converted);
        }
        return (chapters, documentTitle);
    }

    public static string NormalizeHeading(string heading, ChapterHeadingStyle style)
    {
        heading = heading.Trim();
        if (style != ChapterHeadingStyle.EmDash) return heading;
        var m = ChapterPrefix.Match(heading);
        if (!m.Success) return heading;
        var rest = heading[m.Length..].Trim();
        var label = $"{m.Groups[1].Value} {m.Groups[2].Value}";
        return rest.Length == 0 ? label : $"{label} — {rest}";
    }

    private static string StripChapterPrefix(string heading)
    {
        var m = Regex.Match(heading, @"^(Chapter|Appendix|Part)\s+\S+\s*(—|:)\s*");
        return m.Success ? heading[m.Length..] : heading;
    }

    private static IEnumerable<Block> Convert(Markdig.Syntax.Block block, string source, bool hanging)
    {
        switch (block)
        {
            case MdHeading h:
                yield return new Model.HeadingBlock(h.Level, Raw(source, h.Inline), Spans(h.Inline));
                break;
            case MdMath math:
            {
                var latex = string.Join("\n", math.Lines.Lines.Take(math.Lines.Count).Select(l => l.ToString()));
                yield return new Model.MathBlock(latex, LatexMath.ToSpans(latex));
                break;
            }
            // A paragraph that is nothing but one $$…$$ on a single line is display math.
            case MdParagraph p when p.Inline?.FirstChild is MathInline { DelimiterCount: 2 } only && only.NextSibling is null:
            {
                var latex = only.Content.ToString();
                yield return new Model.MathBlock(latex, LatexMath.ToSpans(latex));
                break;
            }
            case MdParagraph p:
                yield return new Paragraph(Raw(source, p.Inline), Spans(p.Inline), hanging ? ParagraphRole.Hanging : ParagraphRole.Body);
                break;
            case MdList list:
            {
                var items = new List<Paragraph>();
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var paragraphs = item.OfType<MdParagraph>().ToList();
                    var raw = string.Join(" ", paragraphs.Select(x => Raw(source, x.Inline)));
                    var spans = new List<ProseInline.Span>();
                    foreach (var x in paragraphs)
                    {
                        if (spans.Count > 0) spans.Add(new ProseInline.Span(" ", ProseInline.Style.None));
                        spans.AddRange(Spans(x.Inline));
                    }
                    items.Add(new Paragraph(raw, spans));
                }
                var start = list.IsOrdered && int.TryParse(list.OrderedStart, out var s) ? s : 1;
                yield return new Model.ListBlock(list.IsOrdered, start, items);
                break;
            }
            case MdQuote quote:
            {
                var inner = new List<Block>();
                foreach (var child in quote) inner.AddRange(Convert(child, source, hanging));
                yield return new Model.QuoteBlock(inner);
                break;
            }
            case Table table:
            {
                var rows = new List<Model.TableRow>();
                foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
                {
                    var cells = new List<Paragraph>();
                    foreach (var cell in row.OfType<TableCell>())
                    {
                        var p = cell.OfType<MdParagraph>().FirstOrDefault();
                        cells.Add(p is null ? new Paragraph("") : new Paragraph(Raw(source, p.Inline), Spans(p.Inline)));
                    }
                    rows.Add(new Model.TableRow(row.IsHeader, cells));
                }
                yield return new TableBlock(rows);
                break;
            }
            case ThematicBreakBlock:
                yield return new RuleBlock();
                break;
            case LeafBlock code when block is CodeBlock:
            {
                var text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(l => l.ToString()));
                yield return new Paragraph(text, [new ProseInline.Span(text, ProseInline.Style.Code)], ParagraphRole.Preformatted);
                break;
            }
            case HtmlBlock:
            case LinkReferenceDefinitionGroup:
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    foreach (var converted in Convert(child, source, hanging))
                        yield return converted;
                break;
        }
    }

    /// <summary>The paragraph's Markdown source on one line (soft breaks become spaces).</summary>
    private static string Raw(string source, ContainerInline? inline)
    {
        if (inline is null) return "";
        var first = inline.FirstChild;
        var last = inline.LastChild;
        if (first is null || last is null) return "";
        var start = first.Span.Start;
        var end = last.Span.End;
        if (start < 0 || end < start || end >= source.Length + 1) return InlineText(inline);
        var raw = source.Substring(start, Math.Min(end - start + 1, source.Length - start));
        return Regex.Replace(raw, @"\s*\n\s*", " ").Trim();
    }

    public static string InlineText(ContainerInline? inline) =>
        string.Concat(Spans(inline).Select(s => s.Text)).Trim();

    public static List<ProseInline.Span> Spans(ContainerInline? inline)
    {
        var spans = new List<ProseInline.Span>();
        if (inline is not null) Walk(inline, ProseInline.Style.None, spans);
        // Merge adjacent runs of the same style.
        var merged = new List<ProseInline.Span>();
        foreach (var span in spans)
        {
            if (span.Text.Length == 0) continue;
            if (merged.Count > 0 && merged[^1].Style == span.Style)
                merged[^1] = merged[^1] with { Text = merged[^1].Text + span.Text };
            else merged.Add(span);
        }
        return merged;
    }

    private static void Walk(Inline inline, ProseInline.Style style, List<ProseInline.Span> spans)
    {
        switch (inline)
        {
            case LiteralInline lit:
                spans.Add(new ProseInline.Span(lit.Content.ToString(), style));
                break;
            case EmphasisInline em:
            {
                var add = em.DelimiterChar == '~' ? ProseInline.Style.Strikethrough
                        : em.DelimiterCount >= 2 ? ProseInline.Style.Bold
                        : ProseInline.Style.Italic;
                foreach (var child in em) Walk(child, style | add, spans);
                break;
            }
            case CodeInline code:
                spans.Add(new ProseInline.Span(code.Content, style | ProseInline.Style.Code));
                break;
            case MathInline math:
                foreach (var span in LatexMath.ToSpans(math.Content.ToString()))
                    spans.Add(span with { Style = span.Style | style });
                break;
            case LinkInline { IsImage: true }:
                break;
            case LinkInline link:
                foreach (var child in link) Walk(child, style, spans);
                break;
            case AutolinkInline auto:
                spans.Add(new ProseInline.Span(auto.Url, style));
                break;
            case LineBreakInline:
                spans.Add(new ProseInline.Span(" ", style));
                break;
            case HtmlEntityInline entity:
                spans.Add(new ProseInline.Span(entity.Transcoded.ToString(), style));
                break;
            case HtmlInline:
                break;
            case ContainerInline container:
                foreach (var child in container) Walk(child, style, spans);
                break;
        }
    }
}

/// <summary>Title-page and catalog metadata for a Markdown-sourced manuscript.</summary>
public sealed record ManuscriptInfo
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string? Slug { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = [];

    public Manuscript ToManuscript(List<Chapter> chapters) => new()
    {
        Title = Title,
        Subtitle = Subtitle,
        Author = Author,
        Description = Description,
        Slug = Slug,
        Keywords = [.. Keywords],
        Chapters = chapters
    };
}
