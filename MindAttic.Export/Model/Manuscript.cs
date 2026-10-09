using MindAttic.Export.Text;

namespace MindAttic.Export.Model;

/// <summary>
/// A renderer-neutral book or document: everything an exporter needs, nothing about where it
/// came from. Prose builds one from its database (beats, spine, glossary); Markdown documents
/// (book reports, the SET dissertation) are read into one by
/// <see cref="Markdown.MarkdownManuscriptReader"/>.
/// </summary>
public sealed class Manuscript
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }

    /// <summary>Catalog blurb: ebook <c>dc:description</c> and <c>description.txt</c>. Never
    /// printed on the title page.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier used in log lines (Prose: the node slug).</summary>
    public string? Slug { get; init; }

    public string Language { get; init; } = "en";

    /// <summary>Chapters in reading order. A <c>null</c> heading prints no heading (a
    /// single-chapter story, or text before the first chapter).</summary>
    public List<Chapter> Chapters { get; init; } = [];

    /// <summary>Back-matter glossary, already sorted. Docx renders it as term/definition pairs;
    /// EPUB and PDF append it as a "Glossary" chapter; TXT and Markdown omit it (Prose house
    /// behaviour).</summary>
    public List<GlossaryEntry> Glossary { get; init; } = [];

    /// <summary>KDP keyword phrases, written one per line to <c>keywords.txt</c>.</summary>
    public List<string> Keywords { get; init; } = [];
}

public sealed record Chapter(string? Heading, List<Block> Blocks)
{
    public Chapter(string? heading) : this(heading, []) { }
}

public sealed record GlossaryEntry(string Term, string? FullForm, string Definition);

/// <summary>One unit of chapter content.</summary>
public abstract record Block;

/// <summary>A body paragraph. <see cref="Text"/> is the source text (Prose inline markers or
/// Markdown inline syntax) and is what the Markdown renderer writes back. When
/// <see cref="Spans"/> is null the styled runs are parsed from <see cref="Text"/> with
/// <see cref="ProseInline"/>.</summary>
public sealed record ParagraphBlock(string Text, IReadOnlyList<ProseInline.Span>? Spans = null,
                                    ParagraphRole Role = ParagraphRole.Body) : Block
{
    public IReadOnlyList<ProseInline.Span> Runs => Spans ?? ProseInline.Parse(Text);
}

public enum ParagraphRole
{
    Body,
    /// <summary>Reference-list entry: hanging indent, left aligned.</summary>
    Hanging,
    /// <summary>Preformatted (code) text: monospace, no justification.</summary>
    Preformatted
}

/// <summary>Prose's mid-chapter sub-heading: centered, bold, no page break, never in the TOC.</summary>
public sealed record SubHeadingBlock(string Text) : Block;

/// <summary>A document heading inside a chapter (Markdown <c>##</c> = level 2, <c>###</c> = 3,
/// <c>####</c> = 4). Left aligned, never in the TOC.</summary>
public sealed record HeadingBlock(int Level, string Text, IReadOnlyList<ProseInline.Span>? Spans = null) : Block
{
    public IReadOnlyList<ProseInline.Span> Runs => Spans ?? ProseInline.Parse(Text);
}

/// <summary>Prose's struck-tenet page: first line is the kanji, struck through, alone on a page;
/// remaining lines are the gloss. Only the docx renderer treats it specially; every other format
/// renders its lines as ordinary paragraphs.</summary>
public sealed record TenetBlock(string Text) : Block;

/// <summary>An invisible round-trip marker written only by the Markdown renderer, e.g.
/// <c>beat:12:019fc3c7…</c> → <c>&lt;!-- beat:12:019fc3c7… --&gt;</c>.</summary>
public sealed record MarkerBlock(string Marker) : Block;

public sealed record ListBlock(bool Ordered, int Start, List<ParagraphBlock> Items) : Block;

public sealed record QuoteBlock(List<Block> Blocks) : Block;

public sealed record TableBlock(List<TableRow> Rows) : Block;

public sealed record TableRow(bool IsHeader, List<ParagraphBlock> Cells);

/// <summary>Display math. <see cref="Latex"/> is kept for the Markdown renderer; the other
/// renderers use <see cref="Spans"/> (Unicode with super/subscript runs).</summary>
public sealed record MathBlock(string Latex, IReadOnlyList<ProseInline.Span> Spans) : Block;

/// <summary>A thematic break (scene or section break).</summary>
public sealed record RuleBlock : Block;
