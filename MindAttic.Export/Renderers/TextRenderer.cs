using System.Text;
using MindAttic.Export.Model;
using MindAttic.Export.Text;

namespace MindAttic.Export.Renderers;

/// <summary>
/// Plain-text manuscript (Prose's audio/narration script): title, optional subtitle and
/// "by Author", then each chapter as a heading line followed by its paragraphs with every inline
/// marker stripped. UTF-8 without BOM, a blank line between paragraphs. Glossary omitted.
/// Migrated from <c>ManuscriptExportService.ExportAudioTxtAsync</c>; document blocks (lists,
/// tables, quotes, math) are additions that Prose books never contain.
/// </summary>
public sealed class TextRenderer : IManuscriptRenderer
{
    public ExportFormat Format => ExportFormat.Txt;

    public async Task<RenderResult> RenderAsync(Manuscript manuscript, string path, ExportOptions options, CancellationToken ct = default)
    {
        var text = Render(manuscript);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false), ct);
        return new RenderResult(path);
    }

    public static string Render(Manuscript manuscript)
    {
        var sb = new StringBuilder();
        sb.AppendLine(manuscript.Title);
        if (!string.IsNullOrWhiteSpace(manuscript.Subtitle))
            sb.AppendLine(manuscript.Subtitle!.Trim());
        if (!string.IsNullOrWhiteSpace(manuscript.Author))
            sb.AppendLine($"by {manuscript.Author!.Trim()}");
        sb.AppendLine();

        foreach (var chapter in manuscript.Chapters)
        {
            if (!string.IsNullOrWhiteSpace(chapter.Heading))
            {
                sb.AppendLine(chapter.Heading!);
                sb.AppendLine();
            }
            foreach (var block in chapter.Blocks)
                AppendBlock(sb, block, indent: "");
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendBlock(StringBuilder sb, Block block, string indent)
    {
        switch (block)
        {
            case ParagraphBlock p:
                sb.AppendLine(indent + (p.Spans is null ? ProseInline.StripFormatting(p.Text) : Plain(p.Spans)));
                sb.AppendLine();
                break;
            case SubHeadingBlock s:
                sb.AppendLine(indent + ProseInline.StripFormatting(s.Text));
                sb.AppendLine();
                break;
            case TenetBlock t:
                foreach (var line in SplitLines(t.Text))
                {
                    sb.AppendLine(indent + ProseInline.StripFormatting(line));
                    sb.AppendLine();
                }
                break;
            case HeadingBlock h:
                sb.AppendLine(indent + Plain(h.Runs));
                sb.AppendLine();
                break;
            case ListBlock l:
                for (var i = 0; i < l.Items.Count; i++)
                    sb.AppendLine(indent + (l.Ordered ? $"{l.Start + i}. " : "- ") + Plain(l.Items[i].Runs));
                sb.AppendLine();
                break;
            case QuoteBlock q:
                foreach (var inner in q.Blocks) AppendBlock(sb, inner, indent + "    ");
                break;
            case TableBlock table:
                foreach (var row in table.Rows)
                    sb.AppendLine(indent + string.Join(" | ", row.Cells.Select(c => Plain(c.Runs))));
                sb.AppendLine();
                break;
            case MathBlock m:
                sb.AppendLine(indent + Plain(m.Spans));
                sb.AppendLine();
                break;
            case RuleBlock:
                sb.AppendLine(indent + "* * *");
                sb.AppendLine();
                break;
            case StanzaBlock stanza:
                foreach (var line in stanza.Lines)
                    sb.AppendLine(indent + ProseInline.StripFormatting(line));
                sb.AppendLine();
                break;
            case ScriptBlock script:
                if (HangsUnderCue(script.Element)) TrimBlankLine(sb);
                sb.AppendLine(indent + ScriptLine(script));
                sb.AppendLine();
                break;
            case MarkerBlock:
                break;
        }
    }

    /// <summary>Dialogue and parentheticals sit directly under their cue, with no blank line.</summary>
    internal static bool HangsUnderCue(ScriptElement element) =>
        element is ScriptElement.Dialogue or ScriptElement.Parenthetical;

    /// <summary>Drop the blank line the previous block wrote, so the next line hangs under it.</summary>
    internal static void TrimBlankLine(StringBuilder sb)
    {
        var blank = Environment.NewLine + Environment.NewLine;
        if (sb.Length >= blank.Length && sb.ToString(sb.Length - blank.Length, blank.Length) == blank)
            sb.Length -= Environment.NewLine.Length;
    }

    /// <summary>A screenplay element as a monospace line: the indents are the standard ones at ten
    /// characters to the inch (cue 2.0", parenthetical 1.5", dialogue 1.0"); transitions are set
    /// flush right on a 60-column line.</summary>
    private static string ScriptLine(ScriptBlock script)
    {
        var text = ProseInline.StripFormatting(script.Text).Trim();
        return script.Element switch
        {
            ScriptElement.SceneHeading => text.ToUpperInvariant(),
            ScriptElement.Character => new string(' ', 20) + text.ToUpperInvariant(),
            ScriptElement.Parenthetical => new string(' ', 15) + text,
            ScriptElement.Dialogue => new string(' ', 10) + text,
            ScriptElement.Transition => text.ToUpperInvariant().PadLeft(60),
            _ => text
        };
    }

    internal static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static readonly Dictionary<char, char> Superscripts = new()
    {
        ['0'] = '⁰', ['1'] = '¹', ['2'] = '²', ['3'] = '³', ['4'] = '⁴', ['5'] = '⁵', ['6'] = '⁶',
        ['7'] = '⁷', ['8'] = '⁸', ['9'] = '⁹', ['+'] = '⁺', ['-'] = '⁻', ['−'] = '⁻', ['='] = '⁼',
        ['('] = '⁽', [')'] = '⁾', ['n'] = 'ⁿ', ['i'] = 'ⁱ', ['*'] = '*', ['r'] = 'ʳ', ['t'] = 'ᵗ'
    };

    private static readonly Dictionary<char, char> Subscripts = new()
    {
        ['0'] = '₀', ['1'] = '₁', ['2'] = '₂', ['3'] = '₃', ['4'] = '₄', ['5'] = '₅', ['6'] = '₆',
        ['7'] = '₇', ['8'] = '₈', ['9'] = '₉', ['+'] = '₊', ['-'] = '₋', ['='] = '₌', ['('] = '₍',
        [')'] = '₎', ['a'] = 'ₐ', ['e'] = 'ₑ', ['h'] = 'ₕ', ['i'] = 'ᵢ', ['j'] = 'ⱼ', ['k'] = 'ₖ',
        ['l'] = 'ₗ', ['m'] = 'ₘ', ['n'] = 'ₙ', ['o'] = 'ₒ', ['p'] = 'ₚ', ['r'] = 'ᵣ', ['s'] = 'ₛ',
        ['t'] = 'ₜ', ['u'] = 'ᵤ', ['v'] = 'ᵥ', ['x'] = 'ₓ'
    };

    /// <summary>Spans as plain text. Super/subscript runs use Unicode modifier characters when
    /// every character has one, and caret/underscore notation otherwise.</summary>
    internal static string Plain(IEnumerable<ProseInline.Span> spans)
    {
        var sb = new StringBuilder();
        foreach (var span in spans)
        {
            if (span.Style.HasFlag(ProseInline.Style.Superscript))
                sb.Append(Map(span.Text, Superscripts, "^"));
            else if (span.Style.HasFlag(ProseInline.Style.Subscript))
                sb.Append(Map(span.Text, Subscripts, "_"));
            else
                sb.Append(span.Text);
        }
        return sb.ToString();
    }

    private static string Map(string text, Dictionary<char, char> table, string marker)
    {
        if (text.All(table.ContainsKey)) return new string(text.Select(c => table[c]).ToArray());
        return text.Length == 1 ? marker + text : $"{marker}({text})";
    }
}
