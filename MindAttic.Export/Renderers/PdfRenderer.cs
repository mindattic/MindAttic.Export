using MindAttic.Export.Model;
using MindAttic.Export.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MindAttic.Export.Renderers;

/// <summary>
/// PDF via QuestPDF. The book path (6" × 9" KDP paperback trim, Garamond, centered title page,
/// one page section per chapter, centered page numbers) is migrated unchanged from
/// <c>ManuscriptExportService.ExportPdfAsync</c>. The Letter profile, the page-numbered contents
/// page and the document blocks are additions for reports and academic documents.
/// </summary>
public sealed class PdfRenderer : IManuscriptRenderer
{
    static PdfRenderer()
    {
        // Community licence, as Prose.Hub sets it at startup. Idempotent.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ExportFormat Format => ExportFormat.Pdf;

    public Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default)
    {
        Render(manuscript, path, options);
        return Task.FromResult(new RenderResult(path));
    }

    public static void Render(Manuscript manuscript, string path, ExportOptions options)
    {
        var chapters = EpubRenderer.WithGlossary(manuscript);
        var author = manuscript.Author;
        var font = options.FontFamily;

        // Trade: 6" × 9" KDP paperback trim (points: 1" = 72pt); margins top/bottom 1",
        // left/right 0.75" symmetric for screen reading. Letter: 8.5" × 11", 1" all round.
        var trade = options.Profile == PageProfile.Trade6x9;
        var trim = trade ? new PageSize(432, 648) : new PageSize(612, 792);
        float marginTop = 72f, marginBottom = 72f;
        float marginLeft = trade ? 54f : 72f, marginRight = trade ? 54f : 72f;

        var toc = options.IncludeToc && chapters.Count(c => !string.IsNullOrWhiteSpace(c.Heading)) >= 2;
        string SectionId(int i) => $"chapter-{i + 1:D3}";

        var document = Document.Create(container =>
        {
            // ── Title page ──
            container.Page(p =>
            {
                p.Size(trim);
                p.MarginTop(marginTop); p.MarginBottom(marginBottom);
                p.MarginLeft(marginLeft); p.MarginRight(marginRight);
                p.PageColor(Colors.White);
                p.DefaultTextStyle(t => t.FontFamily(font).FontSize(12).FontColor(Colors.Black));
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

            // ── Contents (documents only) ──
            if (toc)
            {
                container.Page(p =>
                {
                    p.Size(trim);
                    p.MarginTop(marginTop); p.MarginBottom(marginBottom);
                    p.MarginLeft(marginLeft); p.MarginRight(marginRight);
                    p.PageColor(Colors.White);
                    p.DefaultTextStyle(t => t.FontFamily(font).FontSize(12).LineHeight(1.4f).FontColor(Colors.Black));
                    p.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(18).AlignCenter().Text("Contents").FontSize(16).Bold();
                        for (var i = 0; i < chapters.Count; i++)
                        {
                            if (string.IsNullOrWhiteSpace(chapters[i].Heading)) continue;
                            var id = SectionId(i);
                            col.Item().PaddingBottom(4).SectionLink(id).Row(row =>
                            {
                                row.RelativeItem().Text(chapters[i].Heading!);
                                row.ConstantItem(36).AlignRight().Text(t => t.BeginPageNumberOfSection(id));
                            });
                        }
                    });
                    p.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            }

            // ── Body — one page section per chapter so each chapter starts fresh ──
            for (var ci = 0; ci < chapters.Count; ci++)
            {
                var chapter = chapters[ci];
                var sectionId = SectionId(ci);
                container.Page(p =>
                {
                    p.Size(trim);
                    p.MarginTop(marginTop); p.MarginBottom(marginBottom);
                    p.MarginLeft(marginLeft); p.MarginRight(marginRight);
                    p.PageColor(Colors.White);
                    p.DefaultTextStyle(t => t.FontFamily(font).FontSize(12).LineHeight(1.4f).FontColor(Colors.Black));
                    p.Content().Column(col =>
                    {
                        if (!string.IsNullOrWhiteSpace(chapter.Heading))
                        {
                            var heading = col.Item();
                            if (toc) heading = heading.Section(sectionId);
                            heading.PaddingBottom(18).AlignCenter().Text(chapter.Heading).FontSize(16).Bold();
                        }
                        foreach (var block in chapter.Blocks)
                            AppendBlock(col, block, font);
                    });
                    p.Footer().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                });
            }
        });

        if (options.FixedTimestamp is DateTime fixedAt)
            document = document.WithMetadata(new DocumentMetadata
            {
                Title = manuscript.Title,
                Author = author ?? "",
                CreationDate = new DateTimeOffset(DateTime.SpecifyKind(fixedAt, DateTimeKind.Utc)),
                ModifiedDate = new DateTimeOffset(DateTime.SpecifyKind(fixedAt, DateTimeKind.Utc))
            });

        document.GeneratePdf(path);
    }

    private static void AppendBlock(ColumnDescriptor col, Block block, string font)
    {
        switch (block)
        {
            case SubHeadingBlock s:
                col.Item().PaddingTop(12).PaddingBottom(10).AlignCenter().Text(s.Text).FontSize(13).Bold();
                break;

            case ParagraphBlock { Spans: null, Role: ParagraphRole.Body } p:
                col.Item().PaddingBottom(6).Text(t =>
                {
                    t.Justify();
                    AppendInline(t, p.Text);
                });
                break;

            case ParagraphBlock { Role: ParagraphRole.Preformatted } code:
                col.Item().PaddingBottom(6).Background(Colors.Grey.Lighten4).Padding(6)
                   .Text(code.Text).FontFamily("Consolas").FontSize(9).LineHeight(1.2f);
                break;

            case ParagraphBlock { Role: ParagraphRole.Hanging } hanging:
                // QuestPDF rejects a negative first-line indent, so reference entries are set flush
                // left (not justified) with extra space between entries rather than with a true
                // hanging indent. Docx and EPUB render the hanging indent.
                col.Item().PaddingBottom(8).Text(t =>
                {
                    t.AlignLeft();
                    AppendSpans(t, hanging.Runs);
                });
                break;

            case ParagraphBlock p:
                col.Item().PaddingBottom(6).Text(t =>
                {
                    t.Justify();
                    AppendSpans(t, p.Runs);
                });
                break;

            case TenetBlock t:
                foreach (var line in TextRenderer.SplitLines(t.Text))
                    col.Item().PaddingBottom(6).Text(x =>
                    {
                        x.Justify();
                        AppendInline(x, line);
                    });
                break;

            case HeadingBlock h:
            {
                var size = h.Level <= 2 ? 13.5f : 12f;
                col.Item().PaddingTop(h.Level <= 2 ? 12 : 8).PaddingBottom(4).Text(t =>
                {
                    foreach (var span in h.Runs)
                    {
                        var run = Styled(t.Span(span.Text), span).FontSize(size).Bold();
                        if (h.Level >= 4) run.Italic();
                    }
                });
                break;
            }

            case ListBlock l:
                for (var i = 0; i < l.Items.Count; i++)
                {
                    var item = l.Items[i];
                    var marker = l.Ordered ? $"{l.Start + i}." : "•";
                    col.Item().PaddingBottom(3).Row(row =>
                    {
                        row.ConstantItem(22).Text(marker);
                        row.RelativeItem().Text(t => AppendSpans(t, item.Runs));
                    });
                }
                col.Item().PaddingBottom(4);
                break;

            case QuoteBlock q:
                col.Item().PaddingLeft(24).PaddingRight(24).PaddingVertical(4).Column(inner =>
                {
                    foreach (var b in q.Blocks) AppendBlock(inner, b, font);
                });
                break;

            case TableBlock table:
            {
                var columns = table.Rows.Count == 0 ? 1 : table.Rows.Max(r => r.Cells.Count);
                col.Item().PaddingVertical(6).Table(tb =>
                {
                    // Columns weighted by their longest cell (square-root damped, so a long prose
                    // column gets room without starving the short ones).
                    var weights = Enumerable.Range(0, columns).Select(c =>
                        (float)Math.Sqrt(Math.Max(4, table.Rows.Max(r => c < r.Cells.Count
                            ? string.Concat(r.Cells[c].Runs.Select(s => s.Text)).Length : 0)))).ToArray();
                    tb.ColumnsDefinition(cd => { foreach (var w in weights) cd.RelativeColumn(w); });
                    foreach (var row in table.Rows)
                        for (var c = 0; c < columns; c++)
                        {
                            var cell = c < row.Cells.Count ? row.Cells[c] : new ParagraphBlock("");
                            var container = tb.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4);
                            if (row.IsHeader) container = container.Background(Colors.Grey.Lighten3);
                            container.Text(t =>
                            {
                                foreach (var span in cell.Runs)
                                {
                                    var run = Styled(t.Span(span.Text), span).FontSize(9.5f).LineHeight(1.2f);
                                    if (row.IsHeader) run.Bold();
                                }
                            });
                        }
                });
                break;
            }

            case MathBlock m:
                col.Item().PaddingVertical(6).AlignCenter().Text(t => AppendSpans(t, m.Spans));
                break;

            case RuleBlock:
                col.Item().PaddingVertical(8).AlignCenter().Text("* * *");
                break;

            case MarkerBlock:
                break;
        }
    }

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

    private static void AppendSpans(TextDescriptor t, IEnumerable<ProseInline.Span> spans)
    {
        foreach (var span in spans) Styled(t.Span(span.Text), span);
    }

    private static TextSpanDescriptor Styled(TextSpanDescriptor run, ProseInline.Span span)
    {
        if (span.Style.HasFlag(ProseInline.Style.Bold)) run = run.Bold();
        if (span.Style.HasFlag(ProseInline.Style.Italic)) run = run.Italic();
        if (span.Style.HasFlag(ProseInline.Style.Underline)) run = run.Underline();
        if (span.Style.HasFlag(ProseInline.Style.Strikethrough)) run = run.Strikethrough();
        if (span.Style.HasFlag(ProseInline.Style.Superscript)) run = run.Superscript();
        if (span.Style.HasFlag(ProseInline.Style.Subscript)) run = run.Subscript();
        if (span.Style.HasFlag(ProseInline.Style.Code)) run = run.FontFamily("Consolas");
        return run;
    }
}
