using MindAttic.Export.Metadata;
using MindAttic.Export.Model;
using MindAttic.Export.Paths;
using MindAttic.Export.Renderers;

namespace MindAttic.Export;

/// <summary>A request to export one manuscript as a versioned bundle.</summary>
public sealed record BundleRequest
{
    /// <summary>The bundle folder, e.g. <c>…\ePub\MindAttic\SET</c>.</summary>
    public required string Directory { get; init; }

    /// <summary>File base name; files are <c>"{FileBaseName} V{N}.{ext}"</c>.</summary>
    public required string FileBaseName { get; init; }

    public IReadOnlyList<ExportFormat> Formats { get; init; } =
        [ExportFormat.Docx, ExportFormat.Epub, ExportFormat.Pdf, ExportFormat.Txt, ExportFormat.Md];

    /// <summary>The version to write. Null takes the highest version already in the folder plus
    /// one (for consumers, unlike Prose, that keep no counter of their own).</summary>
    public int? Version { get; init; }

    /// <summary>Archive the previous live bundle into <c>Archives\v&lt;N&gt;</c> before writing, and
    /// the new one after (Prose's "archive, never delete").</summary>
    public bool Archive { get; init; } = true;

    /// <summary>Write <c>description.txt</c> (with the reading-time line) when the manuscript has
    /// a description, and <c>keywords.txt</c> when it has keywords.</summary>
    public bool Sidecars { get; init; } = true;

    public ExportOptions Options { get; init; } = ExportOptions.ProseBook;
}

public sealed record BundleResult(int Version, IReadOnlyDictionary<ExportFormat, string> Files,
                                  string? DescriptionPath, string? KeywordsPath, int WordCount);

/// <summary>
/// Renders a manuscript to every requested format as one versioned bundle. Consumers that need
/// finer control (Prose interleaves its read gate and press recorder between formats) call the
/// renderers directly; this is the whole pipeline for everyone else.
/// </summary>
public sealed class ManuscriptExporter
{
    private readonly IReadOnlyDictionary<ExportFormat, IManuscriptRenderer> renderers;

    public ManuscriptExporter(IEnumerable<IManuscriptRenderer> renderers)
    {
        this.renderers = renderers.ToDictionary(r => r.Format);
    }

    public ManuscriptExporter() : this(DefaultRenderers()) { }

    public static IEnumerable<IManuscriptRenderer> DefaultRenderers() =>
        [new DocxRenderer(), new EpubRenderer(), new PdfRenderer(), new TextRenderer(), new MarkdownRenderer()];

    public IManuscriptRenderer Renderer(ExportFormat format) =>
        renderers.TryGetValue(format, out var r) ? r : throw new NotSupportedException($"No renderer registered for {format}.");

    public async Task<BundleResult> ExportBundleAsync(Manuscript manuscript, BundleRequest request, CancellationToken ct = default)
    {
        System.IO.Directory.CreateDirectory(request.Directory);
        var previous = ExportArchive.CurrentVersion(request.Directory);
        var version = request.Version ?? previous + 1;
        if (request.Archive) ExportArchive.Clean(request.Directory, previous == 0 ? null : previous);

        var files = new Dictionary<ExportFormat, string>();
        foreach (var format in request.Formats.Distinct())
        {
            var path = Path.Combine(request.Directory, ExportPaths.BundleFileName(request.FileBaseName, version, format.Extension()));
            var result = await Renderer(format).RenderAsync(manuscript, path, request.Options, ct);
            files[format] = result.Path;
        }

        var wordCount = WordCount(manuscript);
        string? descriptionPath = null, keywordsPath = null;
        if (request.Sidecars)
        {
            if (!string.IsNullOrWhiteSpace(manuscript.Description))
                descriptionPath = await ReadingInfo.WriteDescriptionAsync(request.Directory,
                    ReadingInfo.WithReadingLine(manuscript.Description, wordCount), ct);
            keywordsPath = await ReadingInfo.WriteKeywordsAsync(request.Directory, manuscript.Keywords, ct);
        }

        if (request.Archive) ExportArchive.ArchiveCurrent(request.Directory, version);
        return new BundleResult(version, files, descriptionPath, keywordsPath, wordCount);
    }

    /// <summary>Words of running text (paragraphs, list items, table cells, quotes, headings).</summary>
    public static int WordCount(Manuscript manuscript)
    {
        var total = 0;
        foreach (var chapter in manuscript.Chapters)
            foreach (var block in chapter.Blocks)
                total += Words(block);
        return total;

        static int Words(Block block) => block switch
        {
            ParagraphBlock p => ReadingInfo.CountWords(string.Concat(p.Runs.Select(s => s.Text))),
            HeadingBlock h => ReadingInfo.CountWords(string.Concat(h.Runs.Select(s => s.Text))),
            SubHeadingBlock s => ReadingInfo.CountWords(s.Text),
            TenetBlock t => ReadingInfo.CountWords(t.Text),
            ListBlock l => l.Items.Sum(i => Words(i)),
            QuoteBlock q => q.Blocks.Sum(Words),
            TableBlock t => t.Rows.Sum(r => r.Cells.Sum(c => Words(c))),
            _ => 0
        };
    }
}
