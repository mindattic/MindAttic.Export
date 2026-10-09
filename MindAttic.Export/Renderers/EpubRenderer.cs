using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Export.Model;
using MindAttic.Export.Text;

namespace MindAttic.Export.Renderers;

/// <summary>
/// EPUB 3 (the KDP ebook upload): <c>mimetype</c> stored first, container, stylesheet, title
/// page, navigation TOC, one XHTML file per chapter, and the OPF package. Migrated from
/// <c>ManuscriptExportService.ExportEpubAsync</c>; book output is unchanged. The glossary, when
/// present, is appended as a final "Glossary" chapter of sub-heading + definition pairs.
/// </summary>
public sealed class EpubRenderer : IManuscriptRenderer
{
    public ExportFormat Format => ExportFormat.Epub;

    public Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default)
    {
        Render(manuscript, path, options);
        return Task.FromResult(new RenderResult(path));
    }

    public static void Render(Manuscript manuscript, string path, ExportOptions options)
    {
        var chapters = WithGlossary(manuscript);
        var authorName = manuscript.Author ?? "";
        var bookUuid = options.BookIdentifier ?? $"urn:uuid:{Guid.NewGuid()}";
        var modified = options.FixedTimestamp ?? DateTime.UtcNow;

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // EPUB spec: mimetype must be the first entry, stored (not deflated).
        var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var s = mimeEntry.Open()) using (var w = new StreamWriter(s, Encoding.ASCII))
            w.Write("application/epub+zip");

        WriteEntry(zip, "META-INF/container.xml", ContainerXml());
        WriteEntry(zip, "OEBPS/styles.css", StylesCss());
        WriteEntry(zip, "OEBPS/title.xhtml", TitlePageXhtml(manuscript, authorName));
        WriteEntry(zip, "OEBPS/toc.xhtml", TocXhtml(manuscript.Title, chapters));

        for (int i = 0; i < chapters.Count; i++)
            WriteEntry(zip, $"OEBPS/chapter-{i + 1:D3}.xhtml", ChapterXhtml(chapters[i], manuscript.Title));

        WriteEntry(zip, "OEBPS/content.opf", ContentOpf(manuscript, chapters, authorName, bookUuid, modified));
    }

    /// <summary>The chapters plus, when the manuscript has a glossary, a back-matter "Glossary"
    /// chapter: one sub-heading per term ("Term — FullForm") followed by its definition.</summary>
    internal static List<Chapter> WithGlossary(Manuscript manuscript)
    {
        var chapters = new List<Chapter>(manuscript.Chapters);
        if (manuscript.Glossary.Count == 0) return chapters;
        var blocks = new List<Block>();
        foreach (var term in manuscript.Glossary)
        {
            var heading = string.IsNullOrWhiteSpace(term.FullForm) ? term.Term : $"{term.Term} — {term.FullForm}";
            blocks.Add(new SubHeadingBlock(heading));
            blocks.Add(new ParagraphBlock(term.Definition));
        }
        chapters.Add(new Chapter("Glossary", blocks));
        return chapters;
    }

    private static string ContainerXml() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private static string StylesCss() => """
        body { font-family: Georgia, "Times New Roman", serif; line-height: 1.55; margin: 1em; }
        h1, h2, h3 { font-family: inherit; line-height: 1.2; }
        h1.book-title { font-size: 2em; margin: 1.5em 0 0.4em; text-align: center; }
        p.book-subtitle { text-align: center; font-size: 1.2em; color: #555; margin: 0 0 1em; }
        p.author { text-align: center; margin-top: 2em; font-size: 1.1em; }
        p.synopsis { text-align: center; color: #666; font-style: italic; margin-top: 1em; }
        body.title-page { text-align: center; }
        h2.chapter-heading { font-size: 1.4em; margin: 2em 0 1em; text-align: center; }
        h3.sub-heading { font-size: 1.1em; margin: 1.6em 0 0.8em; text-align: center; }
        p { margin: 0.4em 0; }
        em { font-style: italic; }
        h3.doc-heading, h4.doc-heading, h5.doc-heading { margin: 1.4em 0 0.6em; }
        p.hanging { padding-left: 2em; text-indent: -2em; }
        p.math { text-align: center; margin: 0.8em 0; }
        pre { font-family: Consolas, monospace; font-size: 0.85em; white-space: pre-wrap; }
        table.doc-table { border-collapse: collapse; margin: 1em 0; font-size: 0.9em; }
        table.doc-table th, table.doc-table td { border: 1px solid #888; padding: 0.3em 0.5em; vertical-align: top; text-align: left; }
        table.doc-table th { background: #eee; }
        blockquote { margin: 1em 2em; }
        p.rule { text-align: center; }
        """;

    private static string TitlePageXhtml(Manuscript m, string author)
    {
        // Synopsis intentionally omitted from the title page (back-cover blurb only);
        // it still ships as the ebook <dc:description> catalog metadata.
        var synopsis = "";
        var subtitleHtml = string.IsNullOrWhiteSpace(m.Subtitle)
            ? ""
            : $"""<p class="book-subtitle">{Esc(m.Subtitle!.Trim())}</p>""";
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en">
            <head><title>{Esc(m.Title)}</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>
            <body class="title-page">
              <h1 class="book-title">{Esc(m.Title)}</h1>
              {subtitleHtml}
              <p class="author">{Esc(author)}</p>{synopsis}
            </body></html>
            """;
    }

    private static string TocXhtml(string title, List<Chapter> chapters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<!DOCTYPE html>""");
        sb.AppendLine("""<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" xml:lang="en">""");
        sb.AppendLine($"""<head><title>{Esc(title)} — Contents</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>""");
        sb.AppendLine("""<body><nav epub:type="toc" id="toc"><h1>Contents</h1><ol>""");
        for (int i = 0; i < chapters.Count; i++)
        {
            // Single-chapter story: heading is null, so the sole TOC entry uses the
            // book title rather than a "Chapter 1" label we never want to print.
            var label = string.IsNullOrWhiteSpace(chapters[i].Heading) ? title : chapters[i].Heading!;
            sb.AppendLine($"""  <li><a href="chapter-{i + 1:D3}.xhtml">{Esc(label)}</a></li>""");
        }
        sb.AppendLine("""</ol></nav></body></html>""");
        return sb.ToString();
    }

    private static string ChapterXhtml(Chapter chapter, string bookTitle)
    {
        // Heading is null for a single-chapter story (never print "Chapter 1") — the
        // page <title> falls back to the book title and no <h2> heading is emitted.
        var heading = string.IsNullOrWhiteSpace(chapter.Heading) ? null : chapter.Heading!.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<!DOCTYPE html>""");
        sb.AppendLine("""<html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en">""");
        sb.AppendLine($"""<head><title>{Esc(heading ?? bookTitle)}</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>""");
        sb.AppendLine("<body>");
        if (heading is not null)
            sb.AppendLine($"""<h2 class="chapter-heading">{Esc(heading)}</h2>""");
        foreach (var block in chapter.Blocks)
            AppendBlock(sb, block);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendBlock(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case SubHeadingBlock s:
                sb.AppendLine($"""<h3 class="sub-heading">{Esc(s.Text)}</h3>""");
                break;
            case ParagraphBlock { Role: ParagraphRole.Preformatted } code:
                sb.AppendLine($"<pre>{Esc(code.Text)}</pre>");
                break;
            case ParagraphBlock { Spans: null, Role: ParagraphRole.Body } p:
                sb.AppendLine($"<p>{RenderInline(p.Text)}</p>");
                break;
            case ParagraphBlock p:
                sb.AppendLine(p.Role == ParagraphRole.Hanging
                    ? $"""<p class="hanging">{RenderSpans(p.Runs)}</p>"""
                    : $"<p>{RenderSpans(p.Runs)}</p>");
                break;
            case TenetBlock t:
                foreach (var line in TextRenderer.SplitLines(t.Text))
                    sb.AppendLine($"<p>{RenderInline(line)}</p>");
                break;
            case HeadingBlock h:
            {
                var tag = $"h{Math.Clamp(h.Level + 1, 3, 5)}";
                sb.AppendLine($"""<{tag} class="doc-heading">{RenderSpans(h.Runs)}</{tag}>""");
                break;
            }
            case ListBlock l:
                sb.AppendLine(l.Ordered ? (l.Start == 1 ? "<ol>" : $"""<ol start="{l.Start}">""") : "<ul>");
                foreach (var item in l.Items) sb.AppendLine($"<li>{RenderSpans(item.Runs)}</li>");
                sb.AppendLine(l.Ordered ? "</ol>" : "</ul>");
                break;
            case QuoteBlock q:
                sb.AppendLine("<blockquote>");
                foreach (var inner in q.Blocks) AppendBlock(sb, inner);
                sb.AppendLine("</blockquote>");
                break;
            case TableBlock t:
                sb.AppendLine("""<table class="doc-table">""");
                foreach (var row in t.Rows)
                {
                    var cell = row.IsHeader ? "th" : "td";
                    sb.Append("<tr>");
                    foreach (var c in row.Cells) sb.Append($"<{cell}>{RenderSpans(c.Runs)}</{cell}>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table>");
                break;
            case MathBlock m:
                sb.AppendLine($"""<p class="math">{RenderSpans(m.Spans)}</p>""");
                break;
            case RuleBlock:
                sb.AppendLine("""<p class="rule">* * *</p>""");
                break;
            case MarkerBlock:
                break;
        }
    }

    private static string ContentOpf(Manuscript m, List<Chapter> chapters, string author, string uuid, DateTime modified)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid" xml:lang="en">""");
        sb.AppendLine("""<metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">""");
        sb.AppendLine($"""  <dc:identifier id="bookid">{uuid}</dc:identifier>""");
        sb.AppendLine($"""  <dc:title>{Esc(m.Title)}</dc:title>""");
        sb.AppendLine($"""  <dc:creator opf:role="aut">{Esc(author)}</dc:creator>""");
        sb.AppendLine("""  <dc:language>en</dc:language>""");
        if (!string.IsNullOrWhiteSpace(m.Description))
            sb.AppendLine($"""  <dc:description>{Esc(m.Description)}</dc:description>""");
        sb.AppendLine($"""  <meta property="dcterms:modified">{modified.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}</meta>""");
        sb.AppendLine("</metadata>");
        sb.AppendLine("<manifest>");
        sb.AppendLine("""  <item id="css"   href="styles.css"  media-type="text/css"/>""");
        sb.AppendLine("""  <item id="title" href="title.xhtml" media-type="application/xhtml+xml"/>""");
        sb.AppendLine("""  <item id="toc"   href="toc.xhtml"   media-type="application/xhtml+xml" properties="nav"/>""");
        for (int i = 0; i < chapters.Count; i++)
            sb.AppendLine($"""  <item id="ch{i + 1:D3}" href="chapter-{i + 1:D3}.xhtml" media-type="application/xhtml+xml"/>""");
        sb.AppendLine("</manifest>");
        sb.AppendLine("<spine>");
        sb.AppendLine("""  <itemref idref="title"/>""");
        sb.AppendLine("""  <itemref idref="toc"/>""");
        for (int i = 0; i < chapters.Count; i++)
            sb.AppendLine($"""  <itemref idref="ch{i + 1:D3}"/>""");
        sb.AppendLine("</spine>");
        sb.AppendLine("</package>");
        return sb.ToString();
    }

    /// <summary>Render the inline markers (<see cref="ProseInline"/>: bold, italic, underline,
    /// strikethrough) as XHTML elements; HTML-escape everything else. Mirrors the .docx export.</summary>
    public static string RenderInline(string text) => RenderSpans(ProseInline.Parse(text));

    public static string RenderSpans(IEnumerable<ProseInline.Span> spans)
    {
        var sb = new StringBuilder();
        foreach (var span in spans)
        {
            var open = new StringBuilder();
            var close = new StringBuilder();
            if (span.Style.HasFlag(ProseInline.Style.Bold)) { open.Append("<strong>"); close.Insert(0, "</strong>"); }
            if (span.Style.HasFlag(ProseInline.Style.Italic)) { open.Append("<em>"); close.Insert(0, "</em>"); }
            if (span.Style.HasFlag(ProseInline.Style.Underline)) { open.Append("<u>"); close.Insert(0, "</u>"); }
            if (span.Style.HasFlag(ProseInline.Style.Strikethrough)) { open.Append("<s>"); close.Insert(0, "</s>"); }
            if (span.Style.HasFlag(ProseInline.Style.Superscript)) { open.Append("<sup>"); close.Insert(0, "</sup>"); }
            if (span.Style.HasFlag(ProseInline.Style.Subscript)) { open.Append("<sub>"); close.Insert(0, "</sub>"); }
            if (span.Style.HasFlag(ProseInline.Style.Code)) { open.Append("<code>"); close.Insert(0, "</code>"); }
            sb.Append(open).Append(Esc(span.Text)).Append(close);
        }
        return sb.ToString();
    }

    // XML 1.0 forbids C0 controls other than tab/LF/CR; one stray \f or \v pasted into a beat made
    // the whole EPUB unparseable.
    private static readonly Regex XmlIllegalChars = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);

    public static string Esc(string? s) =>
        XmlIllegalChars.Replace(s ?? "", "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static void WriteEntry(ZipArchive zip, string entryPath, string content)
    {
        var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var s = entry.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        w.Write(content);
    }
}
