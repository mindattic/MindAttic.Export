using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MindAttic.Export.Metadata;
using MindAttic.Export.Model;
using MindAttic.Export.Text;
using WText = DocumentFormat.OpenXml.Wordprocessing.Text;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace MindAttic.Export.Renderers;

/// <summary>
/// Word <c>.docx</c> in the manuscript shape Kindle Direct Publishing prefers: a title page,
/// every chapter starting on a fresh page under a centered heading, and justified
/// block-paragraph body text (no first-line indent; 8pt after each paragraph) at 1.15 spacing.
/// Migrated from <c>Prose.Core.Services.DocxExportService</c>; the book path is unchanged so a
/// Prose book renders byte-for-byte as before. Document blocks (headings, lists, tables, quotes,
/// math, hanging-indent references), the Letter profile and footer page numbers are additions.
/// </summary>
public sealed class DocxRenderer : IManuscriptRenderer
{
    private const string Body12 = "24";   // half-points → 12pt
    private const string Chapter16 = "32";
    private const string Title28 = "56";
    private const string Subtitle18 = "36";
    private const string Author14 = "28";
    private const string Heading2Size = "26";
    private const string Table10 = "20";
    private const string Mono = "Consolas";

    // Words-per-page base rate, calibrated via least-squares over 7 stories (UNDR, DWIACE, MNEMO,
    // SRZR, MxG, ATTE, TEST): pages ≈ words/306 + chapters*1.1, avg error ±3.6 pages.
    private const double WordsPerPage = 306.0;
    // Average pages lost per chapter (page-break waste + heading height).
    private const double ChapterPageOverhead = 1.1;

    public ExportFormat Format => ExportFormat.Docx;

    public Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default)
        => Task.FromResult(Render(manuscript, path, options));

    public static RenderResult Render(Manuscript manuscript, string path, ExportOptions options)
    {
        var w = new Writer(options.FontFamily);
        var author = manuscript.Author;
        var chapterCount = options.ChapterCount ?? manuscript.Chapters.Count(c => !string.IsNullOrWhiteSpace(c.Heading));
        int wordCount = 0;
        int estimatedPages;

        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            // Explicitly set document metadata so Word doesn't pull Creator from
            // the Windows/Microsoft account of whoever opens the file.
            // Same C0-control scrub as every text run: the package writer throws on save otherwise.
            var creator = author is null ? null : XmlIllegalChars.Replace(author, "");
            doc.PackageProperties.Creator = creator;
            doc.PackageProperties.LastModifiedBy = creator;

            var main = doc.AddMainDocumentPart();
            main.Document = new Document();

            // Styles: Heading1 (chapter headings), TOCHeading, TOC1, Hyperlink.
            // TOCHeading and TOC1 are the named styles Word uses when building a TOC field —
            // without them the pre-populated entries lose formatting on open/update.
            var stylePart = main.AddNewPart<StyleDefinitionsPart>();
            stylePart.Styles = w.Styles();
            stylePart.Styles.Save();

            // Do NOT auto-update fields on open. Our pre-populated TOC entries (hyperlinks +
            // bookmarks) are the display content; if Word recalculates them it replaces our
            // Hyperlink-styled runs with plain text because the auto-update pass doesn't add
            // bookmarks to headings. Users can still press F9 in Word to refresh page numbers.
            // MirrorMargins makes the gutter (inside margin) alternate left/right for recto/verso
            // pages — required for KDP paperback so the gutter is always on the spine side.
            var settingsPart = main.AddNewPart<DocumentSettingsPart>();
            settingsPart.Settings = options.Profile == PageProfile.Trade6x9
                ? new Settings(new MirrorMargins(), new UpdateFieldsOnOpen { Val = false })
                : new Settings(new UpdateFieldsOnOpen { Val = false });
            settingsPart.Settings.Save();

            string? footerId = null;
            if (options.DocxPageNumbers)
            {
                var footerPart = main.AddNewPart<FooterPart>();
                footerPart.Footer = w.PageNumberFooter();
                footerPart.Footer.Save();
                footerId = main.GetIdOfPart(footerPart);
            }

            var body = main.Document.AppendChild(new Body());

            // ── Title page ──
            body.AppendChild(w.BlankLines(8));
            body.AppendChild(w.Centered(manuscript.Title, Title28, bold: true));
            if (!string.IsNullOrWhiteSpace(manuscript.Subtitle))
                body.AppendChild(w.Centered(manuscript.Subtitle!, Subtitle18));
            if (!string.IsNullOrWhiteSpace(author))
                body.AppendChild(w.Centered(author!, Author14, italic: true));
            body.AppendChild(PageBreak());

            // Pre-build the TOC entry list so both the SDT and the chapter headings
            // share the same set of _Toc{N} anchor names.
            // Bookmark ID 0 is reserved for the "toc" anchor on the Contents heading;
            // IDs 1..N go on the chapter headings and match their TOC entry PAGEREFs.
            var tocEntries = new List<(string Title, string Anchor)>();
            foreach (var chapter in manuscript.Chapters)
                if (!string.IsNullOrWhiteSpace(chapter.Heading))
                    tocEntries.Add((chapter.Heading!, TocAnchor(tocEntries.Count)));

            // Glossary gets one more TOC entry + bookmark, appended after every chapter's.
            string? glossaryAnchor = null;
            if (manuscript.Glossary.Count > 0)
            {
                glossaryAnchor = TocAnchor(tocEntries.Count);
                tocEntries.Add(("Glossary", glossaryAnchor));
            }

            // ── Table of Contents (only when enabled and there is more than one chapter) ──
            if (options.IncludeToc && chapterCount >= 2)
            {
                body.AppendChild(w.BuildTocSdt(tocEntries));
                body.AppendChild(PageBreak());
            }

            // ── Body ──
            bool chapterEmitted = false;
            int tocIdx = 0;
            for (int c = 0; c < manuscript.Chapters.Count; c++)
            {
                var chapter = manuscript.Chapters[c];
                // A single-chapter story prints no chapter heading at all — we never
                // emit "Chapter 1". Headings (and their page breaks) only appear when
                // the story actually divides into two or more chapters.
                if (!string.IsNullOrWhiteSpace(chapter.Heading) && chapterCount >= 2)
                {
                    if (chapterEmitted) body.AppendChild(PageBreak());
                    string? anchor = (tocIdx < tocEntries.Count) ? tocEntries[tocIdx].Anchor : null;
                    body.AppendChild(w.ChapterHeading(chapter.Heading!, anchor, bookmarkId: tocIdx + 1));
                    tocIdx++;
                    chapterEmitted = true;
                }

                for (int b = 0; b < chapter.Blocks.Count; b++)
                {
                    var block = chapter.Blocks[b];
                    switch (block)
                    {
                        case SubHeadingBlock s:
                            body.AppendChild(w.SubHeading(s.Text));
                            break;

                        case TenetBlock t:
                        {
                            // A tenet page: the kanji of a broken virtue, struck through, alone on
                            // the page. Claims a whole leaf on purpose — the device is the silence
                            // around it. Deliberately uncounted: a struck page is not prose and must
                            // not inflate the KDP page estimate.
                            var struckText = t.Text.Trim();
                            if (struckText.Length == 0) break;
                            body.AppendChild(PageBreak());
                            foreach (var p in w.TenetPage(struckText)) body.AppendChild(p);
                            // Only break out if the next block does not open a chapter — that one
                            // emits its own break, and two in a row would leave a blank leaf.
                            var nextStartsChapter = b == chapter.Blocks.Count - 1
                                && c + 1 < manuscript.Chapters.Count
                                && !string.IsNullOrWhiteSpace(manuscript.Chapters[c + 1].Heading)
                                && chapterCount >= 2;
                            if (!nextStartsChapter) body.AppendChild(PageBreak());
                            break;
                        }

                        case ParagraphBlock p:
                            wordCount += ReadingInfo.CountWords(p.Spans is null ? p.Text : string.Concat(p.Spans.Select(s => s.Text)));
                            body.AppendChild(w.Paragraph(p));
                            break;

                        default:
                            foreach (var el in w.DocumentBlock(block, ref wordCount)) body.AppendChild(el);
                            break;
                    }
                }
            }

            // ── Back matter: Glossary ──
            if (manuscript.Glossary.Count > 0)
            {
                body.AppendChild(PageBreak());
                body.AppendChild(w.ChapterHeading("Glossary", glossaryAnchor, bookmarkId: tocEntries.Count));
                // Flat alphabetical list — no category grouping (author decision 2026-08-05).
                foreach (var term in manuscript.Glossary)
                {
                    body.AppendChild(w.GlossaryEntryHeading(term.Term, term.FullForm));
                    body.AppendChild(w.BodyParagraph(term.Definition));
                }
            }

            // Estimate KDP page count from word count + chapter overhead; store for gutter selection.
            estimatedPages = Math.Max(1, (int)Math.Round(wordCount / WordsPerPage + chapterCount * ChapterPageOverhead));
            body.AppendChild(SectionProps(options.Profile, estimatedPages, footerId));
            main.Document.Save();
        }

        return new RenderResult(path, wordCount, estimatedPages);
    }

    // ── page geometry ─────────────────────────────────────────────────────────

    // KDP paperback trim: 6" × 9" (8640 × 12960 twips).
    // Left/Right = 720 (0.5" outer). Gutter is calculated from page count via KDP's table.
    // MirrorMargins (set in Settings) flips gutter to spine side on verso.
    private static SectionProperties SectionProps(PageProfile profile, int? kdpPageCount, string? footerId)
    {
        var sect = profile == PageProfile.Trade6x9
            ? new SectionProperties(
                new PageSize { Width = 8640U, Height = 12960U },
                new PageMargin { Top = 1440, Bottom = 1440, Left = 720U, Right = 720U, Header = 720U, Footer = 720U, Gutter = KdpGutter(kdpPageCount) })
            : new SectionProperties(
                new PageSize { Width = 12240U, Height = 15840U },
                new PageMargin { Top = 1440, Bottom = 1440, Left = 1440U, Right = 1440U, Header = 720U, Footer = 720U, Gutter = 0U });
        if (footerId is not null)
            sect.PrependChild(new FooterReference { Type = HeaderFooterValues.Default, Id = footerId });
        return sect;
    }

    // KDP minimum inside (gutter) margin by page count (source: KDP Content Guidelines).
    // Null = unknown page count; falls back to the maximum-safe value (0.875").
    private static uint KdpGutter(int? pageCount) => (pageCount ?? int.MaxValue) switch
    {
        >= 701 => 1260U,  // 0.875"
        >= 601 => 1080U,  // 0.75"
        >= 401 =>  900U,  // 0.625"
        >= 151 =>  720U,  // 0.5"
        _      =>  540U,  // 0.375"
    };

    private static Paragraph PageBreak() => new(new Run(new Break { Type = BreakValues.Page }));

    /// <summary>Deterministic _Toc bookmark name. n is zero-based chapter index.</summary>
    private static string TocAnchor(int n) => $"_Toc{10000 + n}";

    private static readonly System.Text.RegularExpressions.Regex XmlIllegalChars =
        new(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Element builders bound to one base font.</summary>
    private sealed class Writer(string serif)
    {
        private const string Kanji72 = "144";  // half-points → 72pt
        private const string Gloss11 = "22";

        /// <summary>A CJK-capable face for the kanji. Garamond has no glyphs for 礼/義/勇/仁/誠, and a
        /// missing glyph renders as a box — which would put a row of tofu where the whole device is.</summary>
        private const string KanjiFont = "Yu Mincho";

        public Styles Styles() => new(
            new Style(
                new StyleName { Val = "heading 1" },
                new BasedOn { Val = "Normal" },
                new NextParagraphStyle { Val = "Normal" },
                new UIPriority { Val = 9 },
                new PrimaryStyle(),
                new StyleParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines { Before = "480", After = "360" },
                    new Justification { Val = JustificationValues.Center },
                    new OutlineLevel { Val = 0 }),
                new StyleRunProperties(
                    new RunFonts { Ascii = serif, HighAnsi = serif, ComplexScript = serif },
                    new Bold(),
                    new FontSize { Val = Chapter16 },
                    new FontSizeComplexScript { Val = Chapter16 }))
            { Type = StyleValues.Paragraph, StyleId = "Heading1" },

            // TOCHeading — the "Contents" title paragraph style.
            // outlineLvl=9 prevents it from appearing in its own TOC field.
            new Style(
                new StyleName { Val = "TOC Heading" },
                new BasedOn { Val = "Heading1" },
                new NextParagraphStyle { Val = "Normal" },
                new UIPriority { Val = 39 },
                new UnhideWhenUsed(),
                new PrimaryStyle(),
                new StyleParagraphProperties(
                    new KeepLines(),
                    new SpacingBetweenLines { Before = "240", After = "0", Line = "259", LineRule = LineSpacingRuleValues.Auto },
                    new Justification { Val = JustificationValues.Left },
                    new OutlineLevel { Val = 9 }),
                new StyleRunProperties(
                    new RunFonts { Ascii = serif, HighAnsi = serif, ComplexScript = serif },
                    new Bold(),
                    new FontSize { Val = Chapter16 },
                    new FontSizeComplexScript { Val = Chapter16 }))
            { Type = StyleValues.Paragraph, StyleId = "TOCHeading" },

            // TOC1 — one entry per Heading 1.
            // autoRedefine: Word rewrites this style when it rebuilds the TOC field.
            new Style(
                new StyleName { Val = "toc 1" },
                new BasedOn { Val = "Normal" },
                new NextParagraphStyle { Val = "Normal" },
                new AutoRedefine(),
                new UIPriority { Val = 39 },
                new UnhideWhenUsed(),
                new StyleParagraphProperties(
                    new SpacingBetweenLines { After = "100" }))
            { Type = StyleValues.Paragraph, StyleId = "TOC1" },

            // Hyperlink character style — applied to TOC entry text runs.
            new Style(
                new StyleName { Val = "Hyperlink" },
                new BasedOn { Val = "DefaultParagraphFont" },
                new UIPriority { Val = 99 },
                new UnhideWhenUsed(),
                new StyleRunProperties(
                    new Color { Val = "467886" },
                    new Underline { Val = UnderlineValues.Single }))
            { Type = StyleValues.Character, StyleId = "Hyperlink" });

        public Footer PageNumberFooter()
        {
            var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            p.AppendChild(new Run(new RunProperties(Fonts(), new FontSize { Val = "18" }), new FieldChar { FieldCharType = FieldCharValues.Begin }));
            p.AppendChild(new Run(new RunProperties(Fonts(), new FontSize { Val = "18" }), new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }));
            p.AppendChild(new Run(new RunProperties(Fonts(), new FontSize { Val = "18" }), new FieldChar { FieldCharType = FieldCharValues.Separate }));
            p.AppendChild(new Run(new RunProperties(Fonts(), new FontSize { Val = "18" }), new WText("1")));
            p.AppendChild(new Run(new RunProperties(Fonts(), new FontSize { Val = "18" }), new FieldChar { FieldCharType = FieldCharValues.End }));
            return new Footer(p);
        }

        private RunFonts Fonts() => new() { Ascii = serif, HighAnsi = serif, ComplexScript = serif };

        public Paragraph BlankLines(int n)
        {
            var p = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            for (int i = 0; i < n; i++) p.AppendChild(MakeRun("", Body12));
            return p;
        }

        /// <summary>
        /// The struck-tenet page. First line of the beat is the kanji; any remaining lines are the
        /// gloss beneath it (romaji and the English virtue). Centered, pushed down the page, the kanji
        /// struck through — the tenet is legible and cancelled at the same time, which is the point.
        /// </summary>
        public IEnumerable<Paragraph> TenetPage(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length == 0) yield break;

            // Roughly a third of the way down a 9" page, so the mark sits in the optical centre.
            yield return new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { Before = "3600", After = "0" }));

            var kanji = new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "360" },
                new Justification { Val = JustificationValues.Center }));
            kanji.AppendChild(KanjiRun(lines[0]));
            yield return kanji;

            for (var i = 1; i < lines.Length; i++)
                yield return Centered(lines[i], Gloss11, italic: true);
        }

        private static Run KanjiRun(string text)
        {
            var rPr = new RunProperties(
                new RunFonts { Ascii = KanjiFont, HighAnsi = KanjiFont, EastAsia = KanjiFont, ComplexScript = KanjiFont },
                new Strike(),
                new FontSize { Val = Kanji72 },
                new FontSizeComplexScript { Val = Kanji72 });
            var run = new Run(rPr);
            run.AppendChild(new WText(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        public Paragraph Centered(string text, string halfPt, bool bold = false, bool italic = false) =>
            new(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                MakeRun(text, halfPt, bold, italic));

        /// <summary>Chapter heading with an optional <c>_Toc{N}</c> bookmark so the pre-built
        /// TOC hyperlinks and Word's PAGEREF fields resolve correctly on open.</summary>
        public Paragraph ChapterHeading(string text, string? tocAnchor = null, int bookmarkId = 1)
        {
            var p = new Paragraph(new ParagraphProperties(
                new ParagraphStyleId { Val = "Heading1" },
                new KeepNext(),
                new SpacingBetweenLines { Before = "480", After = "360" },
                new Justification { Val = JustificationValues.Center }));
            if (tocAnchor != null)
                p.AppendChild(new BookmarkStart { Id = bookmarkId.ToString(), Name = tocAnchor });
            p.AppendChild(MakeRun(text, Chapter16, bold: true));
            if (tocAnchor != null)
                p.AppendChild(new BookmarkEnd { Id = bookmarkId.ToString() });
            return p;
        }

        /// <summary>Mid-chapter sub-heading — centered, bold, smaller than a chapter heading, no
        /// page break, no TOC bookmark, no <c>Heading1</c> style.</summary>
        public Paragraph SubHeading(string text) =>
            new(new ParagraphProperties(
                    new KeepNext(),
                    new SpacingBetweenLines { Before = "360", After = "160" },
                    new Justification { Val = JustificationValues.Center }),
                MakeRun(text, Body12, bold: true));

        /// <summary>
        /// A Structured Document Tag containing a pre-populated Word TOC field — the same
        /// structure Word produces when you insert a Table of Contents and then update it. Each
        /// entry is a hyperlink anchoring to the _Toc{N} bookmark on the chapter heading; PAGEREF
        /// fields carry a placeholder "1" until the reader presses F9.
        /// </summary>
        public SdtBlock BuildTocSdt(List<(string Title, string Anchor)> entries)
        {
            var sdt = new SdtBlock();

            sdt.AppendChild(new SdtProperties(
                new SdtAlias { Val = "Table of Contents" },
                new Tag { Val = "Table of Contents" }));

            // End-of-SDT run formatting (matches V13 reference document).
            sdt.AppendChild(new SdtEndCharProperties(
                new RunProperties(
                    new RunFonts { Ascii = serif, HighAnsi = serif, ComplexScript = serif },
                    new FontSize { Val = Body12 },
                    new FontSizeComplexScript { Val = Body12 })));

            var sdtContent = new SdtContentBlock();

            // "Contents" heading — TOCHeading style + KDP "toc" navigation bookmark.
            var headPara = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "TOCHeading" }));
            headPara.AppendChild(new BookmarkStart { Id = "0", Name = "toc" });
            headPara.AppendChild(MakeRun("Contents", Chapter16, bold: true));
            headPara.AppendChild(new BookmarkEnd { Id = "0" });
            sdtContent.AppendChild(headPara);
            sdtContent.AppendChild(new Paragraph()); // blank line between heading and first entry

            // One TOC1 paragraph per chapter entry. The first paragraph carries fldChar:begin +
            // instrText + fldChar:separate before its hyperlink; the rest continue the same field.
            for (int i = 0; i < entries.Count; i++)
            {
                var (title, anchor) = entries[i];
                var p = new Paragraph(new ParagraphProperties(
                    new ParagraphStyleId { Val = "TOC1" },
                    new Tabs(new TabStop
                    {
                        Val = TabStopValues.Right,
                        Leader = TabStopLeaderCharValues.Dot,
                        Position = 9350
                    })));

                if (i == 0)
                {
                    p.AppendChild(RunNP(new FieldChar { FieldCharType = FieldCharValues.Begin }));
                    p.AppendChild(RunNP(new FieldCode(" TOC \\o \"1-1\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve }));
                    p.AppendChild(RunNP(new FieldChar { FieldCharType = FieldCharValues.Separate }));
                }

                p.AppendChild(TocHyperlink(title, anchor));
                sdtContent.AppendChild(p);
            }

            // Final paragraph closes the outer TOC field.
            sdtContent.AppendChild(new Paragraph(
                RunNP(new RunProperties(new Bold(), new BoldComplexScript()),
                      new FieldChar { FieldCharType = FieldCharValues.End })));

            sdt.AppendChild(sdtContent);
            return sdt;
        }

        /// <summary>One TOC entry: chapter title as a hyperlink + a hidden PAGEREF for page number.</summary>
        private static Hyperlink TocHyperlink(string title, string anchor)
        {
            var link = new Hyperlink { Anchor = anchor, History = new OnOffValue(true) };

            link.AppendChild(new Run(
                new RunProperties(new RunStyle { Val = "Hyperlink" }, new NoProof()),
                // Same C0-control scrub as MakeRun: the package writer throws on save otherwise.
                new WText(XmlIllegalChars.Replace(title ?? "", "")) { Space = SpaceProcessingModeValues.Preserve }));

            // Tab + PAGEREF — webHidden so they are invisible in eBook/HTML layout.
            link.AppendChild(RunHW(new TabChar()));
            link.AppendChild(RunHW(new FieldChar { FieldCharType = FieldCharValues.Begin }));
            link.AppendChild(RunHW(new FieldCode($" PAGEREF {anchor} \\h ") { Space = SpaceProcessingModeValues.Preserve }));
            link.AppendChild(RunHW()); // empty run between instrText and separate (matches Word output)
            link.AppendChild(RunHW(new FieldChar { FieldCharType = FieldCharValues.Separate }));
            link.AppendChild(new Run(new RunProperties(new NoProof(), new WebHidden()), new WText("1")));
            link.AppendChild(RunHW(new FieldChar { FieldCharType = FieldCharValues.End }));

            return link;
        }

        /// <summary>Run with NoProof, then content elements.</summary>
        private static Run RunNP(params OpenXmlElement[] children)
        {
            var r = new Run(new RunProperties(new NoProof()));
            foreach (var c in children) r.AppendChild(c);
            return r;
        }

        private static Run RunNP(RunProperties extraRpr, params OpenXmlElement[] children)
        {
            extraRpr.AppendChild(new NoProof()); // CT_RPr: b, bCs, then noProof
            var r = new Run(extraRpr);
            foreach (var c in children) r.AppendChild(c);
            return r;
        }

        /// <summary>Run with NoProof + WebHidden (page-number parts invisible in eBook layout).</summary>
        private static Run RunHW(params OpenXmlElement[] children)
        {
            var r = new Run(new RunProperties(new NoProof(), new WebHidden()));
            foreach (var c in children) r.AppendChild(c);
            return r;
        }

        /// <summary>One glossary entry's term line: bold term, then its full expansion (if any)
        /// after an em dash in italic. Left-justified — reference text, not a page title.</summary>
        public Paragraph GlossaryEntryHeading(string term, string? fullForm)
        {
            var p = new Paragraph(new ParagraphProperties(
                new KeepNext(),
                new SpacingBetweenLines { Before = "240", After = "40" },
                new Justification { Val = JustificationValues.Left }));
            p.AppendChild(MakeRun(term, Body12, bold: true));
            if (!string.IsNullOrWhiteSpace(fullForm))
                p.AppendChild(MakeRun($" — {fullForm}", Body12, italic: true));
            return p;
        }

        public Paragraph BodyParagraph(string text)
        {
            var p = new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { Line = "276", LineRule = LineSpacingRuleValues.Auto, After = "160" },
                new Justification { Val = JustificationValues.Both }));
            foreach (var run in InlineRuns(text)) p.AppendChild(run);
            return p;
        }

        /// <summary>A model paragraph: the Prose body paragraph unless it carries its own spans
        /// or a non-body role.</summary>
        public Paragraph Paragraph(ParagraphBlock block)
        {
            if (block.Spans is null && block.Role == ParagraphRole.Body) return BodyParagraph(block.Text);

            ParagraphProperties props = block.Role switch
            {
                ParagraphRole.Hanging => new ParagraphProperties(
                    new SpacingBetweenLines { Line = "276", LineRule = LineSpacingRuleValues.Auto, After = "160" },
                    new Indentation { Left = "720", Hanging = "720" },
                    new Justification { Val = JustificationValues.Left }),
                ParagraphRole.Preformatted => new ParagraphProperties(
                    new SpacingBetweenLines { After = "160" },
                    new Justification { Val = JustificationValues.Left }),
                _ => new ParagraphProperties(
                    new SpacingBetweenLines { Line = "276", LineRule = LineSpacingRuleValues.Auto, After = "160" },
                    new Justification { Val = JustificationValues.Both })
            };
            var p = new Paragraph(props);
            if (block.Role == ParagraphRole.Preformatted)
            {
                var lines = block.Text.Replace("\r\n", "\n").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var r = MakeRun(lines[i], "20", font: Mono);
                    if (i > 0) r.InsertAfter(new Break(), r.RunProperties);
                    p.AppendChild(r);
                }
                return p;
            }
            foreach (var run in SpanRuns(block.Runs, Body12)) p.AppendChild(run);
            return p;
        }

        /// <summary>Document-only blocks (Markdown input). Prose books never contain these.</summary>
        public IEnumerable<OpenXmlElement> DocumentBlock(Block block, ref int wordCount)
        {
            var output = new List<OpenXmlElement>();
            switch (block)
            {
                case HeadingBlock h:
                {
                    var size = h.Level <= 2 ? Heading2Size : Body12;
                    var p = new Paragraph(new ParagraphProperties(
                        new KeepNext(),
                        new SpacingBetweenLines { Before = h.Level <= 2 ? "360" : "240", After = "120" },
                        new Justification { Val = JustificationValues.Left },
                        new OutlineLevel { Val = Math.Clamp(h.Level - 1, 1, 8) }));
                    foreach (var run in SpanRuns(h.Runs, size, forceBold: true, forceItalic: h.Level >= 4)) p.AppendChild(run);
                    output.Add(p);
                    break;
                }
                case ListBlock l:
                    for (var i = 0; i < l.Items.Count; i++)
                    {
                        var item = l.Items[i];
                        wordCount += ReadingInfo.CountWords(string.Concat(item.Runs.Select(s => s.Text)));
                        var p = new Paragraph(new ParagraphProperties(
                            new Tabs(new TabStop { Val = TabStopValues.Left, Position = 720 }),
                            new SpacingBetweenLines { Line = "276", LineRule = LineSpacingRuleValues.Auto, After = "80" },
                            new Indentation { Left = "720", Hanging = "360" },
                            new Justification { Val = JustificationValues.Left }));
                        p.AppendChild(MakeRun(l.Ordered ? $"{l.Start + i}." : "•", Body12));
                        p.AppendChild(new Run(new TabChar()));
                        foreach (var run in SpanRuns(item.Runs, Body12)) p.AppendChild(run);
                        output.Add(p);
                    }
                    break;
                case QuoteBlock q:
                    foreach (var inner in q.Blocks)
                    {
                        if (inner is ParagraphBlock qp)
                        {
                            wordCount += ReadingInfo.CountWords(string.Concat(qp.Runs.Select(s => s.Text)));
                            var p = new Paragraph(new ParagraphProperties(
                                new SpacingBetweenLines { Line = "276", LineRule = LineSpacingRuleValues.Auto, Before = "80", After = "160" },
                                new Indentation { Left = "720", Right = "720" },
                                new Justification { Val = JustificationValues.Both }));
                            foreach (var run in SpanRuns(qp.Runs, Body12)) p.AppendChild(run);
                            output.Add(p);
                        }
                        else output.AddRange(DocumentBlock(inner, ref wordCount));
                    }
                    break;
                case TableBlock t:
                    output.Add(Table(t, ref wordCount));
                    output.Add(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "120" })));
                    break;
                case MathBlock m:
                {
                    var p = new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { Before = "120", After = "200" },
                        new Justification { Val = JustificationValues.Center }));
                    foreach (var run in SpanRuns(m.Spans, Body12)) p.AppendChild(run);
                    output.Add(p);
                    break;
                }
                case RuleBlock:
                    output.Add(Centered("* * *", Body12));
                    break;
            }
            return output;
        }

        private Table Table(TableBlock t, ref int wordCount)
        {
            var columns = t.Rows.Count == 0 ? 1 : t.Rows.Max(r => r.Cells.Count);
            var table = new Table(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    // Schema order: top, left, bottom, right, insideH, insideV.
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "808080" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "808080" }),
                new TableCellMarginDefault(
                    new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new StartMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new EndMargin { Width = "100", Type = TableWidthUnitValues.Dxa })));
            var grid = new TableGrid();
            for (var c = 0; c < columns; c++) grid.AppendChild(new GridColumn());
            table.AppendChild(grid);

            foreach (var row in t.Rows)
            {
                var tr = new TableRow();
                if (row.IsHeader) tr.AppendChild(new TableRowProperties(new TableHeader()));
                for (var c = 0; c < columns; c++)
                {
                    var cellBlock = c < row.Cells.Count ? row.Cells[c] : new ParagraphBlock("");
                    wordCount += ReadingInfo.CountWords(string.Concat(cellBlock.Runs.Select(s => s.Text)));
                    var p = new Paragraph(new ParagraphProperties(
                        new SpacingBetweenLines { After = "0" },
                        new Justification { Val = JustificationValues.Left }));
                    foreach (var run in SpanRuns(cellBlock.Runs, Table10, forceBold: row.IsHeader)) p.AppendChild(run);
                    var cellProps = new TableCellProperties();
                    if (row.IsHeader) cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "EDEDED" });
                    tr.AppendChild(new TableCell(cellProps, p));
                }
                table.AppendChild(tr);
            }
            return table;
        }

        private IEnumerable<Run> InlineRuns(string text)
        {
            // Was: text.Split('*') alternating italic. That read "**SCREEN TEXT**" as two empty
            // segments around a plain one. ProseInline is the single parser the editor shares.
            var runs = new List<Run>();
            foreach (var span in ProseInline.Parse(text))
            {
                if (span.Text.Length == 0) continue;
                runs.Add(MakeRun(span.Text, Body12,
                                 bold: span.Style.HasFlag(ProseInline.Style.Bold),
                                 italic: span.Style.HasFlag(ProseInline.Style.Italic),
                                 underline: span.Style.HasFlag(ProseInline.Style.Underline),
                                 strike: span.Style.HasFlag(ProseInline.Style.Strikethrough)));
            }
            if (runs.Count == 0) runs.Add(MakeRun(text, Body12));
            return runs;
        }

        private IEnumerable<Run> SpanRuns(IEnumerable<ProseInline.Span> spans, string halfPt, bool forceBold = false, bool forceItalic = false)
        {
            foreach (var span in spans)
            {
                if (span.Text.Length == 0) continue;
                var run = MakeRun(span.Text, halfPt,
                    bold: forceBold || span.Style.HasFlag(ProseInline.Style.Bold),
                    italic: forceItalic || span.Style.HasFlag(ProseInline.Style.Italic),
                    underline: span.Style.HasFlag(ProseInline.Style.Underline),
                    strike: span.Style.HasFlag(ProseInline.Style.Strikethrough),
                    font: span.Style.HasFlag(ProseInline.Style.Code) ? Mono : null);
                if (span.Style.HasFlag(ProseInline.Style.Superscript))
                    run.RunProperties!.AppendChild(new VerticalTextAlignment { Val = VerticalPositionValues.Superscript });
                else if (span.Style.HasFlag(ProseInline.Style.Subscript))
                    run.RunProperties!.AppendChild(new VerticalTextAlignment { Val = VerticalPositionValues.Subscript });
                yield return run;
            }
        }

        private Run MakeRun(string text, string halfPt, bool bold = false, bool italic = false,
                            bool underline = false, bool strike = false, string? font = null)
        {
            var face = font ?? serif;
            // CT_RPr order: rFonts, b, i, strike, sz, szCs, u.
            var rPr = new RunProperties(new RunFonts { Ascii = face, HighAnsi = face, ComplexScript = face });
            if (bold) rPr.AppendChild(new Bold());
            if (italic) rPr.AppendChild(new Italic());
            if (strike) rPr.AppendChild(new Strike());
            rPr.AppendChild(new FontSize { Val = halfPt });
            rPr.AppendChild(new FontSizeComplexScript { Val = halfPt });
            if (underline) rPr.AppendChild(new Underline { Val = UnderlineValues.Single });
            var run = new Run(rPr);
            // XML 1.0 forbids C0 controls other than tab/LF/CR; the package writer throws on save.
            run.AppendChild(new WText(XmlIllegalChars.Replace(text ?? "", "")) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }
    }
}
