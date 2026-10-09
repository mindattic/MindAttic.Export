using MindAttic.Export.Model;
using ProseLegacy;

namespace MindAttic.Export.Tests.LegacyEquivalence;

/// <summary>
/// How Prose must build a <see cref="Manuscript"/> from its spine so the new renderers reproduce
/// the legacy files. This is the contract the equivalence tests pin down: each legacy exporter
/// assembled its chapters slightly differently, so there is one adapter per family.
/// <list type="bullet">
/// <item><b>Docx</b> (DocxExportService): spine headings as-is, raw (untrimmed) sub-heading titles,
/// entity tags stripped, tenet beats as <see cref="TenetBlock"/>, <c>ChapterCount</c> = spine count,
/// <c>IncludeToc</c> = settings.DocxIncludeToc.</item>
/// <item><b>Epub/Pdf/Txt</b> (ManuscriptExportService.LoadAsync): a single chapter's heading
/// nulled, blank headings in a multi-chapter book replaced by "Chapter N", sub-heading titles
/// trimmed, entity tags stripped.</item>
/// <item><b>Md</b> (ExportMarkdownAsync): headings only for a multi-chapter book, raw sub-heading
/// titles, entity tags NOT stripped, a <c>beat:N:id</c> marker before every non-empty beat.</item>
/// </list>
/// </summary>
public static class ProseAdapter
{
    public static string ResolveAuthor(string? author) =>
        string.IsNullOrWhiteSpace(author) ? "MindAttic" : author.Trim();

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsTenet(LegacyBeat beat) => string.Equals(beat.Kind, "tenet", StringComparison.OrdinalIgnoreCase);

    private static List<GlossaryEntry> Glossary(LegacyBook book) =>
        book.Glossary.Select(t => new GlossaryEntry(t.Term, t.FullForm, t.Definition)).ToList();

    public static (Manuscript Manuscript, ExportOptions Options) ForDocx(LegacyBook book)
    {
        var chapters = new List<Chapter>();
        foreach (var unit in book.Chapters)
        {
            var blocks = new List<Block>();
            for (var b = 0; b < unit.Beats.Count; b++)
            {
                var beat = unit.Beats[b];
                if (b > 0 && beat.SubHeadingTitle is not null) blocks.Add(new SubHeadingBlock(beat.SubHeadingTitle));
                var text = LegacyBeatMarkup.StripEntityTags(beat.Text).Trim();
                if (text.Length == 0) continue;
                if (IsTenet(beat)) blocks.Add(new TenetBlock(text));
                else blocks.AddRange(SplitParagraphs(text).Select(p => new ParagraphBlock(p)));
            }
            chapters.Add(new Chapter(unit.Heading, blocks));
        }
        var manuscript = new Manuscript
        {
            Title = book.Title,
            Subtitle = book.Subtitle,
            Author = ResolveAuthor(book.Author),
            Description = book.Description,
            Slug = book.Slug,
            Chapters = chapters,
            Glossary = Glossary(book)
        };
        return (manuscript, ExportOptions.ProseBook with { ChapterCount = book.ChapterCount, IncludeToc = book.IncludeToc });
    }

    /// <summary>The LoadAsync family: epub, pdf, audio txt.</summary>
    public static Manuscript ForLoad(LegacyBook book, bool tenetBlocks = true)
    {
        var chapters = new List<Chapter>();
        foreach (var unit in book.Chapters)
        {
            var blocks = new List<Block>();
            foreach (var beat in unit.Beats)
            {
                if (beat.SubHeadingTitle is not null) blocks.Add(new SubHeadingBlock(beat.SubHeadingTitle.Trim()));
                var text = LegacyBeatMarkup.StripEntityTags(beat.Text).Trim();
                if (text.Length == 0) continue;
                if (tenetBlocks && IsTenet(beat)) blocks.Add(new TenetBlock(text));
                else blocks.AddRange(SplitParagraphs(text).Select(p => new ParagraphBlock(p)));
            }
            chapters.Add(new Chapter(unit.Heading, blocks));
        }
        if (chapters.Count == 1)
            chapters[0] = chapters[0] with { Heading = null };
        else
            for (var i = 0; i < chapters.Count; i++)
                if (string.IsNullOrWhiteSpace(chapters[i].Heading))
                    chapters[i] = chapters[i] with { Heading = $"Chapter {i + 1}" };

        return new Manuscript
        {
            Title = book.Title,
            Subtitle = book.Subtitle,
            Author = ResolveAuthor(book.Author),
            Description = book.Description,
            Slug = book.Slug,
            Chapters = chapters,
            Glossary = Glossary(book)
        };
    }

    public static Manuscript ForMarkdown(LegacyBook book, bool tenetBlocks = true)
    {
        var multiChapter = book.ChapterCount > 1;
        var chapters = new List<Chapter>();
        var beatNo = 0;
        foreach (var unit in book.Chapters)
        {
            var blocks = new List<Block>();
            for (var b = 0; b < unit.Beats.Count; b++)
            {
                var beat = unit.Beats[b];
                var opensChapter = b == 0 && multiChapter;
                if (!opensChapter && beat.SubHeadingTitle is not null) blocks.Add(new SubHeadingBlock(beat.SubHeadingTitle));
                var text = (beat.Text ?? "").Trim();
                if (text.Length == 0) continue;
                beatNo++;
                blocks.Add(new MarkerBlock($"beat:{beatNo}:{beat.Id:N}"));
                if (tenetBlocks && IsTenet(beat)) blocks.Add(new TenetBlock(text));
                else blocks.AddRange(SplitParagraphs(text).Select(p => new ParagraphBlock(p)));
            }
            chapters.Add(new Chapter(multiChapter && unit.Beats.Count > 0 ? unit.Heading : null, blocks));
        }
        return new Manuscript
        {
            Title = book.Title,
            Subtitle = book.Subtitle,
            Author = ResolveAuthor(book.Author),
            Description = book.Description,
            Slug = book.Slug,
            Chapters = chapters,
            Glossary = Glossary(book)
        };
    }
}
