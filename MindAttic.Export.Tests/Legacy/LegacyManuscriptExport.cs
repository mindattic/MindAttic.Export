// FROZEN LEGACY COPY — do not edit.
// Prose.Core.Services.ManuscriptExportService (Prose commit e6dc3c37e, the last pre-migration
// version), re-expressed over an in-memory LegacyBook. Removed ONLY: the DbContext/workbench/
// spine/settings/glossary/Claude/read-gate/recorder/logger dependencies, export-path resolution,
// and the ArchivedBooks row. Guid.NewGuid()/DateTime.UtcNow go through LegacySeams. The bodies of
// the four Export*Async methods and of LoadAsync's beat walk are otherwise verbatim, and every
// builder from "StripInlineMarkup" through "EpubWriteEntry", the private records,
// BuildGlossaryChapter, SplitParagraphs and AppendInline are copied unchanged.
using System.IO.Compression;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProseLegacy;

public static class LegacyManuscriptExport
{
    static LegacyManuscriptExport()
    {
        // Prose.Hub sets this at startup.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>ExportMarkdownAsync up to the File.WriteAllTextAsync; returns mdText.</summary>
    public static string ExportMarkdown(LegacyBook book)
    {
        string? author = null;
        // Resolution order: explicit param → node.Author → "MindAttic" (pen name)
        author = string.IsNullOrWhiteSpace(author)
            ? (string.IsNullOrWhiteSpace(book.Author) ? "MindAttic" : book.Author!.Trim())
            : author.Trim();
        var ordered = book.Ordered;

        var md = new StringBuilder();
        md.AppendLine($"# {book.Title}");
        if (!string.IsNullOrWhiteSpace(book.Subtitle))
            md.AppendLine($"### {book.Subtitle!.Trim()}");
        md.AppendLine();
        if (!string.IsNullOrWhiteSpace(author))
        {
            md.AppendLine($"_by {author!.Trim()}_");
            md.AppendLine();
        }

        // spine = book.Chapters
        bool multiChapter = book.ChapterCount > 1;
        var headingAt = new Dictionary<Guid, string>();
        var subHeadingAt = new Dictionary<Guid, string>();
        foreach (var chapter in book.Chapters)
        {
            if (multiChapter && chapter.Beats.Count > 0)
                headingAt[chapter.Beats[0].Id] = chapter.Heading!;
            foreach (var beat in chapter.Beats)
                if (beat.IsSubHeading && beat.SubHeadingTitle is not null)
                    subHeadingAt[beat.Id] = beat.SubHeadingTitle;
        }

        int beatNo = 0;
        foreach (var beat in ordered)
        {
            if (headingAt.TryGetValue(beat.Id, out var heading))
            {
                md.AppendLine($"## {heading}");
                md.AppendLine();
            }
            else if (subHeadingAt.TryGetValue(beat.Id, out var subHeading))
            {
                // Genuine mid-chapter sub-heading — its own heading text, not a new chapter.
                md.AppendLine($"### {subHeading}");
                md.AppendLine();
            }
            var text = (beat.Text ?? "").Trim();
            if (text.Length == 0) continue;
            beatNo++;
            // Full 32-char id: batch-created GUIDv7 beats share long time-ordered
            // prefixes, so a 7-char prefix is ambiguous for --import-md.
            md.AppendLine($"<!-- beat:{beatNo}:{beat.Id:N} -->");
            foreach (var para in SplitParagraphs(text))
            {
                md.AppendLine(para);
                md.AppendLine();
            }
        }

        var mdText = md.ToString().TrimEnd() + "\n";
        return mdText;
    }

    /// <summary>ExportPdfAsync without the read gate and recorder.</summary>
    public static void ExportPdf(LegacyBook book, string path)
    {
        var manuscript = Load(book);
        string? author = null;
        // Resolution order: explicit param → node.Author (via manuscript) → "MindAttic" (pen name)
        author = string.IsNullOrWhiteSpace(author)
            ? (string.IsNullOrWhiteSpace(manuscript.Author) ? "MindAttic" : manuscript.Author!.Trim())
            : author.Trim();

        // Back-matter glossary — same subset DocxExportService already appends (SS-LAW-20:
        // never interrupt in-voice prose to spell out an acronym). PDF/EPUB never had this;
        // only .docx did, which is not what KDP actually ingests for the ebook.
        var glossaryTerms = book.Glossary;
        if (glossaryTerms.Count > 0)
            manuscript.Chapters.Add(BuildGlossaryChapter(glossaryTerms));

        // 6" × 9" KDP paperback trim (points: 1" = 72pt).
        // Margins: top/bottom 1", left/right 0.75" symmetric for screen reading.
        var trim = new PageSize(432, 648);
        const float marginTop = 72f, marginBottom = 72f, marginLeft = 54f, marginRight = 54f;

        QuestPDF.Fluent.Document.Create(container =>
        {
            // ── Title page ──
            container.Page(p =>
            {
                p.Size(trim);
                p.MarginTop(marginTop); p.MarginBottom(marginBottom);
                p.MarginLeft(marginLeft); p.MarginRight(marginRight);
                p.PageColor(Colors.White);
                p.DefaultTextStyle(t => t.FontFamily("Garamond").FontSize(12).FontColor(Colors.Black));
                p.Content().AlignCenter().AlignMiddle().Column(col =>
                {
                    col.Item().Text(manuscript.Title).FontSize(28).Bold();
                    if (!string.IsNullOrWhiteSpace(manuscript.Subtitle))
                        col.Item().PaddingTop(8).Text(manuscript.Subtitle!.Trim()).FontSize(16).FontColor(Colors.Grey.Darken2);
                    if (!string.IsNullOrWhiteSpace(author))
                        col.Item().PaddingTop(24).Text(author!.Trim()).FontSize(14).Italic().FontColor(Colors.Grey.Darken1);
                    // Synopsis intentionally omitted from the title page (back-cover blurb only).
                });
            });

            // ── Body — one page section per chapter so each chapter starts fresh ──
            foreach (var chapter in manuscript.Chapters)
            {
                container.Page(p =>
                {
                    p.Size(trim);
                    p.MarginTop(marginTop); p.MarginBottom(marginBottom);
                    p.MarginLeft(marginLeft); p.MarginRight(marginRight);
                    p.PageColor(Colors.White);
                    p.DefaultTextStyle(t => t.FontFamily("Garamond").FontSize(12).LineHeight(1.4f).FontColor(Colors.Black));
                    p.Content().Column(col =>
                    {
                        if (!string.IsNullOrWhiteSpace(chapter.Heading))
                            col.Item().PaddingBottom(18).AlignCenter().Text(chapter.Heading).FontSize(16).Bold();
                        foreach (var block in chapter.Blocks)
                        {
                            if (block.IsSubHeading)
                                col.Item().PaddingTop(12).PaddingBottom(10).AlignCenter().Text(block.Text).FontSize(13).Bold();
                            else
                                col.Item().PaddingBottom(6).Text(t =>
                                {
                                    t.Justify();
                                    AppendInline(t, block.Text);
                                });
                        }
                    });
                    p.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            }
        }).GeneratePdf(path);
    }

    /// <summary>ExportEpubAsync without the read gate and recorder; Guid.NewGuid() and
    /// DateTime.UtcNow come from <paramref name="seams"/>.</summary>
    public static void ExportEpub(LegacyBook book, string path, LegacySeams seams)
    {
        var manuscript = Load(book);
        string? author = null;
        // Resolution order: explicit param → node.Author (via manuscript) → "MindAttic" (pen name)
        author = string.IsNullOrWhiteSpace(author)
            ? (string.IsNullOrWhiteSpace(manuscript.Author) ? "MindAttic" : manuscript.Author!.Trim())
            : author.Trim();
        var authorName = author;
        var bookUuid = $"urn:uuid:{seams.BookUuid}";

        // Back-matter glossary — same subset DocxExportService already appends.
        var glossaryTerms = book.Glossary;
        if (glossaryTerms.Count > 0)
            manuscript.Chapters.Add(BuildGlossaryChapter(glossaryTerms));

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        // EPUB spec: mimetype must be the first entry, stored (not deflated).
        var mimeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var s = mimeEntry.Open()) using (var w = new StreamWriter(s, Encoding.ASCII))
            w.Write("application/epub+zip");

        EpubWriteEntry(zip, "META-INF/container.xml", EpubContainerXml());
        EpubWriteEntry(zip, "OEBPS/styles.css", EpubStylesCss());
        EpubWriteEntry(zip, "OEBPS/title.xhtml", EpubTitlePageXhtml(manuscript, authorName));
        EpubWriteEntry(zip, "OEBPS/toc.xhtml", EpubTocXhtml(manuscript));

        for (int i = 0; i < manuscript.Chapters.Count; i++)
            EpubWriteEntry(zip, $"OEBPS/chapter-{i + 1:D3}.xhtml", EpubChapterXhtml(manuscript.Chapters[i], manuscript.Title));

        EpubWriteEntry(zip, "OEBPS/content.opf", EpubContentOpf(manuscript, authorName, bookUuid, seams));
        zip.Dispose();   // finish the archive before the press is recorded
        fs.Dispose();
    }

    /// <summary>ExportAudioTxtAsync up to File.WriteAllTextAsync; returns the file text.</summary>
    public static string ExportAudioTxt(LegacyBook book)
    {
        var manuscript = Load(book);
        string? author = null;
        // Resolution order: explicit param → node.Author (via manuscript) → "MindAttic" (pen name)
        author = string.IsNullOrWhiteSpace(author)
            ? (string.IsNullOrWhiteSpace(manuscript.Author) ? "MindAttic" : manuscript.Author!.Trim())
            : author.Trim();

        var sb = new StringBuilder();
        sb.AppendLine(manuscript.Title);
        if (!string.IsNullOrWhiteSpace(manuscript.Subtitle))
            sb.AppendLine(manuscript.Subtitle!.Trim());
        if (!string.IsNullOrWhiteSpace(author))
            sb.AppendLine($"by {author!.Trim()}");
        sb.AppendLine();

        for (int i = 0; i < manuscript.Chapters.Count; i++)
        {
            var chapter = manuscript.Chapters[i];
            if (!string.IsNullOrWhiteSpace(chapter.Heading))
            {
                sb.AppendLine(chapter.Heading!);
                sb.AppendLine();
            }
            foreach (var block in chapter.Blocks)
            {
                sb.AppendLine(StripInlineMarkup(block.Text));
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    /// <summary>Strip every inline marker (see <see cref="ProseInline"/>) for clean narration text.
    /// Unmatched asterisks stay: they are real characters in the prose.</summary>
    private static string StripInlineMarkup(string text) => ProseInline.StripFormatting(text);

    // ── EPUB builders ────────────────────────────────────────────────────────

    private static string EpubContainerXml() => """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private static string EpubStylesCss() => """
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
        """;

    private static string EpubTitlePageXhtml(Manuscript m, string author)
    {
        // Synopsis intentionally omitted from the title page (back-cover blurb only);
        // it still ships as the ebook <dc:description> catalog metadata.
        var synopsis = "";
        var subtitleHtml = string.IsNullOrWhiteSpace(m.Subtitle)
            ? ""
            : $"""<p class="book-subtitle">{EpubEsc(m.Subtitle!.Trim())}</p>""";
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en">
            <head><title>{EpubEsc(m.Title)}</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>
            <body class="title-page">
              <h1 class="book-title">{EpubEsc(m.Title)}</h1>
              {subtitleHtml}
              <p class="author">{EpubEsc(author)}</p>{synopsis}
            </body></html>
            """;
    }

    private static string EpubTocXhtml(Manuscript m)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<!DOCTYPE html>""");
        sb.AppendLine("""<html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops" xml:lang="en">""");
        sb.AppendLine($"""<head><title>{EpubEsc(m.Title)} — Contents</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>""");
        sb.AppendLine("""<body><nav epub:type="toc" id="toc"><h1>Contents</h1><ol>""");
        for (int i = 0; i < m.Chapters.Count; i++)
        {
            // Single-chapter story: heading is null, so the sole TOC entry uses the
            // book title rather than a "Chapter 1" label we never want to print.
            var label = string.IsNullOrWhiteSpace(m.Chapters[i].Heading)
                ? m.Title : m.Chapters[i].Heading!;
            sb.AppendLine($"""  <li><a href="chapter-{i + 1:D3}.xhtml">{EpubEsc(label)}</a></li>""");
        }
        sb.AppendLine("""</ol></nav></body></html>""");
        return sb.ToString();
    }

    private static string EpubChapterXhtml(Chapter chapter, string bookTitle)
    {
        // Heading is null for a single-chapter story (never print "Chapter 1") — the
        // page <title> falls back to the book title and no <h2> heading is emitted.
        var heading = string.IsNullOrWhiteSpace(chapter.Heading) ? null : chapter.Heading!.Trim();
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<!DOCTYPE html>""");
        sb.AppendLine("""<html xmlns="http://www.w3.org/1999/xhtml" xml:lang="en">""");
        sb.AppendLine($"""<head><title>{EpubEsc(heading ?? bookTitle)}</title><link rel="stylesheet" type="text/css" href="styles.css"/></head>""");
        sb.AppendLine("<body>");
        if (heading is not null)
            sb.AppendLine($"""<h2 class="chapter-heading">{EpubEsc(heading)}</h2>""");
        foreach (var block in chapter.Blocks)
        {
            if (block.IsSubHeading)
                sb.AppendLine($"""<h3 class="sub-heading">{EpubEsc(block.Text)}</h3>""");
            else
                sb.AppendLine($"<p>{EpubRenderInline(block.Text)}</p>");
        }
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string EpubContentOpf(Manuscript m, string author, string uuid, LegacySeams seams)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine("""<package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="bookid" xml:lang="en">""");
        sb.AppendLine("""<metadata xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:opf="http://www.idpf.org/2007/opf">""");
        sb.AppendLine($"""  <dc:identifier id="bookid">{uuid}</dc:identifier>""");
        sb.AppendLine($"""  <dc:title>{EpubEsc(m.Title)}</dc:title>""");
        sb.AppendLine($"""  <dc:creator opf:role="aut">{EpubEsc(author)}</dc:creator>""");
        sb.AppendLine("""  <dc:language>en</dc:language>""");
        if (!string.IsNullOrWhiteSpace(m.Description))
            sb.AppendLine($"""  <dc:description>{EpubEsc(m.Description)}</dc:description>""");
        sb.AppendLine($"""  <meta property="dcterms:modified">{seams.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)}</meta>""");
        sb.AppendLine("</metadata>");
        sb.AppendLine("<manifest>");
        sb.AppendLine("""  <item id="css"   href="styles.css"  media-type="text/css"/>""");
        sb.AppendLine("""  <item id="title" href="title.xhtml" media-type="application/xhtml+xml"/>""");
        sb.AppendLine("""  <item id="toc"   href="toc.xhtml"   media-type="application/xhtml+xml" properties="nav"/>""");
        for (int i = 0; i < m.Chapters.Count; i++)
            sb.AppendLine($"""  <item id="ch{i + 1:D3}" href="chapter-{i + 1:D3}.xhtml" media-type="application/xhtml+xml"/>""");
        sb.AppendLine("</manifest>");
        sb.AppendLine("<spine>");
        sb.AppendLine("""  <itemref idref="title"/>""");
        sb.AppendLine("""  <itemref idref="toc"/>""");
        for (int i = 0; i < m.Chapters.Count; i++)
            sb.AppendLine($"""  <itemref idref="ch{i + 1:D3}"/>""");
        sb.AppendLine("</spine>");
        sb.AppendLine("</package>");
        return sb.ToString();
    }

    /// <summary>Render the inline markers (<see cref="ProseInline"/>: bold, italic, underline,
    /// strikethrough) as XHTML elements; HTML-escape everything else. Mirrors the .docx export.</summary>
    internal static string EpubRenderInline(string text)
    {
        var sb = new StringBuilder();
        foreach (var span in ProseInline.Parse(text))
        {
            var open = new StringBuilder();
            var close = new StringBuilder();
            if (span.Style.HasFlag(ProseInline.Style.Bold)) { open.Append("<strong>"); close.Insert(0, "</strong>"); }
            if (span.Style.HasFlag(ProseInline.Style.Italic)) { open.Append("<em>"); close.Insert(0, "</em>"); }
            if (span.Style.HasFlag(ProseInline.Style.Underline)) { open.Append("<u>"); close.Insert(0, "</u>"); }
            if (span.Style.HasFlag(ProseInline.Style.Strikethrough)) { open.Append("<s>"); close.Insert(0, "</s>"); }
            sb.Append(open).Append(EpubEsc(span.Text)).Append(close);
        }
        return sb.ToString();
    }

    // XML 1.0 forbids C0 controls other than tab/LF/CR; one stray \f or \v pasted into a beat made
    // the whole EPUB unparseable.
    private static readonly System.Text.RegularExpressions.Regex XmlIllegalChars =
        new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string EpubEsc(string s) =>
        XmlIllegalChars.Replace(s ?? "", "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static void EpubWriteEntry(ZipArchive zip, string entryPath, string content)
    {
        var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
        using var s = entry.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        w.Write(content);
    }

    private sealed record Manuscript(string Title, string? Subtitle, string Slug, string? Description, string? Author, List<Chapter> Chapters);
    private sealed record Chapter(string? Heading, List<ContentBlock> Blocks);
    /// <summary>One rendered unit of chapter content: either an ordinary body paragraph
    /// (<c>IsSubHeading=false</c>) or a genuine mid-chapter sub-heading like "Three Barrels"
    /// (<c>IsSubHeading=true</c>) — rendered in its own smaller heading style but never
    /// counted, paginated, or spine/TOC-listed as a chapter in its own right.</summary>
    private sealed record ContentBlock(bool IsSubHeading, string Text);

    /// <summary>Builds the back-matter "Glossary" chapter — one sub-heading-styled block per
    /// term ("Term — FullForm", mirroring DocxExportService.GlossaryEntryHeading's bold-term/
    /// italic-fullform pairing) followed by its definition as an ordinary block. Terms arrive
    /// pre-sorted alphabetically from GlossaryService, so no extra sort is needed here.</summary>
    private static Chapter BuildGlossaryChapter(IReadOnlyList<LegacyGlossaryTerm> terms)
    {
        var blocks = new List<ContentBlock>();
        foreach (var term in terms)
        {
            var heading = string.IsNullOrWhiteSpace(term.FullForm) ? term.Term : $"{term.Term} — {term.FullForm}";
            blocks.Add(new ContentBlock(true, heading));
            blocks.Add(new ContentBlock(false, term.Definition));
        }
        return new Chapter("Glossary", blocks);
    }

    /// <summary>LoadAsync's beat walk and heading resolution (the DB/path half removed).</summary>
    private static Manuscript Load(LegacyBook book)
    {
        var ordered = book.Ordered;
        var beatsById = ordered.DistinctBy(o => o.Id).ToDictionary(o => o.Id, o => o); // a beat linked to two nodes walks twice

        var chapters = new List<Chapter>();
        foreach (var unit in book.Chapters)
        {
            var current = new Chapter(unit.Heading, new List<ContentBlock>());
            chapters.Add(current);

            foreach (var spineBeat in unit.Beats)
            {
                if (!beatsById.TryGetValue(spineBeat.Id, out var beat)) continue;

                // Genuine mid-chapter sub-heading — its own heading text, not a new chapter. The
                // spine has already excluded the chapter's opening beat, which is where the old
                // `else if` did that job.
                if (spineBeat.IsSubHeading && spineBeat.SubHeadingTitle is not null)
                    current.Blocks.Add(new ContentBlock(true, spineBeat.SubHeadingTitle.Trim()));

                var text = LegacyBeatMarkup.StripEntityTags(beat.Text).Trim();
                if (text.Length == 0) continue;
                foreach (var para in SplitParagraphs(text))
                    current.Blocks.Add(new ContentBlock(false, para));
            }
        }

        // Resolve the final display heading for every chapter, centrally. A story
        // that resolves to a SINGLE chapter prints no heading at all (Heading = null)
        // — we never print "Chapter 1". Multi-chapter books fill any untitled chapter
        // with its ordinal. Renderers emit the heading verbatim and skip it when null.
        if (chapters.Count == 1)
        {
            chapters[0] = chapters[0] with { Heading = null };
        }
        else
        {
            for (int i = 0; i < chapters.Count; i++)
                if (string.IsNullOrWhiteSpace(chapters[i].Heading))
                    chapters[i] = chapters[i] with { Heading = $"Chapter {i + 1}" };
        }

        return new Manuscript(book.Title, book.Subtitle, book.Slug, book.Description, book.Author, chapters);
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Emit a paragraph into a QuestPDF text block, rendering the inline markers
    /// (<see cref="ProseInline"/>) as styled runs (mirrors the .docx export).</summary>
    private static void AppendInline(TextDescriptor t, string text)
    {
        foreach (var span in ProseInline.Parse(text))
        {
            var run = t.Span(span.Text);
            if (span.Style.HasFlag(ProseInline.Style.Bold)) run = run.Bold();
            if (span.Style.HasFlag(ProseInline.Style.Italic)) run = run.Italic();
            if (span.Style.HasFlag(ProseInline.Style.Underline)) run = run.Underline();
            if (span.Style.HasFlag(ProseInline.Style.Strikethrough)) run = run.Strikethrough();
        }
    }
}
