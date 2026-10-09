using MindAttic.Export.Model;

namespace MindAttic.Export.Renderers;

/// <summary>Renders a <see cref="Manuscript"/> to one file format.</summary>
public interface IManuscriptRenderer
{
    ExportFormat Format { get; }

    Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default);
}

/// <summary>What a render produced. <see cref="WordCount"/> and <see cref="EstimatedPages"/> are
/// filled by the docx renderer (Prose stores the estimate as the node's KDP page count).</summary>
public sealed record RenderResult(string Path, int? WordCount = null, int? EstimatedPages = null);
