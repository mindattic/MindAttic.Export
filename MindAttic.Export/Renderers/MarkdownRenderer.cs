using System.Text;
using MindAttic.Export.Model;

namespace MindAttic.Export.Renderers;

/// <summary>
/// Markdown export. For Prose books this is the round-trip backup format: <c># Title</c>,
/// <c>### Subtitle</c>, <c>_by Author_</c>, then <c>## Chapter</c> headings, <c>### sub-heading</c>
/// lines, <c>&lt;!-- beat:N:id --&gt;</c> markers and paragraphs, exactly as
/// <c>ManuscriptExportService.ExportMarkdownAsync</c> wrote them. Paragraph text is written as
/// stored (its inline markers intact). Document blocks are written as ordinary Markdown.
/// </summary>
public sealed class MarkdownRenderer : IManuscriptRenderer
{
    public ExportFormat Format => ExportFormat.Md;

    public async Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(path, Render(manuscript), new UTF8Encoding(false), ct);
        return new RenderResult(path);
    }

    public static string Render(Manuscript manuscript)
    {
        var md = new StringBuilder();
        md.AppendLine($"# {manuscript.Title}");
        if (!string.IsNullOrWhiteSpace(manuscript.Subtitle))
            md.AppendLine($"### {manuscript.Subtitle!.Trim()}");
        md.AppendLine();
        if (!string.IsNullOrWhiteSpace(manuscript.Author))
        {
            md.AppendLine($"_by {manuscript.Author!.Trim()}_");
            md.AppendLine();
        }

        foreach (var chapter in manuscript.Chapters)
        {
            if (!string.IsNullOrWhiteSpace(chapter.Heading))
            {
                md.AppendLine($"## {chapter.Heading}");
                md.AppendLine();
            }
            foreach (var block in chapter.Blocks)
                AppendBlock(md, block, prefix: "");
        }

        return md.ToString().TrimEnd() + "\n";
    }

    private static void AppendBlock(StringBuilder md, Block block, string prefix)
    {
        switch (block)
        {
            case MarkerBlock m:
                md.AppendLine($"{prefix}<!-- {m.Marker} -->");
                break;
            case SubHeadingBlock s:
                md.AppendLine($"{prefix}### {s.Text}");
                md.AppendLine(prefix.TrimEnd());
                break;
            case ParagraphBlock { Role: ParagraphRole.Preformatted } code:
                md.AppendLine(prefix + "```");
                foreach (var line in code.Text.Replace("\r\n", "\n").Split('\n'))
                    md.AppendLine(prefix + line);
                md.AppendLine(prefix + "```");
                md.AppendLine(prefix.TrimEnd());
                break;
            case ParagraphBlock p:
                md.AppendLine(prefix + p.Text);
                md.AppendLine(prefix.TrimEnd());
                break;
            case TenetBlock t:
                foreach (var line in TextRenderer.SplitLines(t.Text))
                {
                    md.AppendLine(prefix + line);
                    md.AppendLine(prefix.TrimEnd());
                }
                break;
            case HeadingBlock h:
                md.AppendLine($"{prefix}{new string('#', Math.Clamp(h.Level + 1, 3, 6))} {h.Text}");
                md.AppendLine(prefix.TrimEnd());
                break;
            case ListBlock l:
                for (var i = 0; i < l.Items.Count; i++)
                    md.AppendLine(prefix + (l.Ordered ? $"{l.Start + i}. " : "- ") + l.Items[i].Text);
                md.AppendLine(prefix.TrimEnd());
                break;
            case QuoteBlock q:
                foreach (var inner in q.Blocks) AppendBlock(md, inner, prefix + "> ");
                break;
            case TableBlock table:
                var header = table.Rows.FirstOrDefault(r => r.IsHeader) ?? table.Rows.FirstOrDefault();
                if (header is null) break;
                md.AppendLine(prefix + Row(header));
                md.AppendLine(prefix + "|" + string.Concat(header.Cells.Select(_ => "---|")));
                foreach (var row in table.Rows.Where(r => !ReferenceEquals(r, header)))
                    md.AppendLine(prefix + Row(row));
                md.AppendLine(prefix.TrimEnd());
                break;
            case MathBlock math:
                md.AppendLine(prefix + "$$");
                md.AppendLine(prefix + math.Latex.Trim());
                md.AppendLine(prefix + "$$");
                md.AppendLine(prefix.TrimEnd());
                break;
            case RuleBlock:
                md.AppendLine(prefix + "---");
                md.AppendLine(prefix.TrimEnd());
                break;
        }
    }

    private static string Row(TableRow row) =>
        "| " + string.Join(" | ", row.Cells.Select(c => c.Text.Replace("|", "\\|"))) + " |";
}
