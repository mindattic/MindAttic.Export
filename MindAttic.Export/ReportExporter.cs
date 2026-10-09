using MindAttic.Export.Artifacts;
using MindAttic.Export.Markdown;
using MindAttic.Export.Renderers;

namespace MindAttic.Export;

/// <summary>
/// Exports a single Markdown report (a Prose book report, an audit, any generated report) to a
/// set of formats beside each other: <c>{stem}.md</c>, <c>{stem}.txt</c>, <c>{stem}.docx</c>,
/// <c>{stem}.pdf</c>, <c>{stem}.epub</c>. The Markdown is written exactly as given; the other
/// formats are rendered from it in the Letter document style. The first level-1 heading becomes
/// the title page.
/// </summary>
public static class ReportExporter
{
    public static async Task<IReadOnlyDictionary<ExportFormat, string>> ExportAsync(
        string markdown,
        string directory,
        string fileStem,
        IEnumerable<ExportFormat> formats,
        string fallbackTitle,
        string? subtitle = null,
        string? author = null,
        ArtifactOptions? artifactOptions = null,
        CancellationToken ct = default)
    {
        artifactOptions ??= ArtifactOptions.Default;
        var options = ExportOptions.Document with { IncludeToc = false };
        var manuscript = MarkdownManuscriptReader.ReadReport(markdown, fallbackTitle, subtitle, author);
        var written = new Dictionary<ExportFormat, string>();

        foreach (var format in formats.Distinct())
        {
            var name = $"{fileStem}.{format.Extension()}";
            written[format] = format switch
            {
                ExportFormat.Md => await ArtifactWriter.WriteTextAsync(directory, name, markdown, artifactOptions, ct),
                ExportFormat.Txt => await ArtifactWriter.WriteTextAsync(directory, name, TextRenderer.Render(manuscript), artifactOptions, ct),
                ExportFormat.Docx => await ArtifactWriter.WriteViaPathAsync(directory, name,
                    (path, _) => { DocxRenderer.Render(manuscript, path, options); return Task.CompletedTask; }, artifactOptions, ct),
                ExportFormat.Pdf => await ArtifactWriter.WriteViaPathAsync(directory, name,
                    (path, _) => { PdfRenderer.Render(manuscript, path, options); return Task.CompletedTask; }, artifactOptions, ct),
                ExportFormat.Epub => await ArtifactWriter.WriteViaPathAsync(directory, name,
                    (path, _) => { EpubRenderer.Render(manuscript, path, options); return Task.CompletedTask; }, artifactOptions, ct),
                _ => throw new NotSupportedException(format.ToString())
            };
        }
        return written;
    }
}
