using System.Text;

namespace MindAttic.Export.Tests.Support;

/// <summary>A generated Markdown document and what reading it must produce.</summary>
public sealed record GeneratedDocument(string Markdown, List<string> ExpectedHeadings, bool HasPreamble, int Tables, int DisplayMath)
{
    public override string ToString() => $"doc headings={ExpectedHeadings.Count} preamble={HasPreamble} tables={Tables} math={DisplayMath}";
}

/// <summary>Seeded Markdown documents (reports, dissertations) using every block the reader
/// understands: headings, tables, lists, quotes, code, rules, math, reference chapters.</summary>
public static class DocumentGen
{
    private static readonly string[] Latex =
    [
        "x^2 + y^2 = z^2", @"\frac{a}{b}", @"\sum_i x_i", @"\alpha \cdot \beta", @"E = mc^2", @"\sqrt{2}", @"V_{\text{max}}",
        @"\bar{x} \pm \sigma", @"P(A|B) = \frac{P(B|A)P(A)}{P(B)}", @"\int_0^1 f(x)\,dx"
    ];

    private static string Inline(TextGen g, bool math)
    {
        var sb = new StringBuilder();
        var n = g.R.Next(4, 30);
        for (var i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            var w = g.Words(1).Replace("$", "");
            sb.Append(g.R.Next(12) switch
            {
                0 => $"**{w}**",
                1 => $"*{w}*",
                2 => $"`{w}`",
                3 when math => $"${g.Pick(Latex)}$",
                4 => $"~~{w}~~",
                _ => w
            });
        }
        return sb.Append('.').ToString();
    }

    public static GeneratedDocument Generate(int seed, bool math = true)
    {
        var g = new TextGen(seed * 31 + 7, TextFeatures.Unicode);
        var md = new StringBuilder();
        var headings = new List<string>();
        var tables = 0;
        var display = 0;
        var preamble = g.Chance(0.3);
        if (preamble) md.Append(Inline(g, math)).Append("\n\n");

        var chapters = g.R.Next(1, 7);
        for (var c = 0; c < chapters; c++)
        {
            var title = string.Join(' ', g.Words(g.R.Next(1, 4)).Split(' ').Select(w => w.Trim('.', '*', '`', '$', '#')).Where(w => w.Length > 0));
            if (title.Length == 0) title = "Untitled";
            var references = c == chapters - 1 && g.Chance(0.3);
            string written, expected;
            if (references) { written = expected = "References"; }
            else switch (g.R.Next(4))
            {
                case 0: written = $"Chapter {c + 1}: {title}"; expected = $"Chapter {c + 1} — {title}"; break;
                case 1: written = $"Appendix {(char)('A' + c)}: {title}"; expected = $"Appendix {(char)('A' + c)} — {title}"; break;
                case 2: written = $"Part {c + 1}: {title}"; expected = $"Part {c + 1} — {title}"; break;
                default: written = expected = title; break;
            }
            headings.Add(expected);
            md.Append("# ").Append(written).Append("\n\n");

            if (references)
            {
                for (var i = g.R.Next(1, 5); i > 0; i--)
                    md.Append($"Author, {g.Title(1)}. ({1990 + g.R.Next(35)}). *{g.Title(3)}*. Publisher.\n\n");
                continue;
            }

            for (var b = g.R.Next(1, 9); b > 0; b--)
            {
                switch (g.R.Next(11))
                {
                    case 0: md.Append("## ").Append(g.Title(3).Replace("#", "")).Append("\n\n"); break;
                    case 1: md.Append("### ").Append(g.Title(3).Replace("#", "")).Append("\n\n"); break;
                    case 2:
                    {
                        var cols = g.R.Next(1, 5);
                        md.Append('|').Append(string.Concat(Enumerable.Range(0, cols).Select(i => $" H{i} |"))).Append('\n');
                        md.Append('|').Append(string.Concat(Enumerable.Range(0, cols).Select(_ => "---|"))).Append('\n');
                        for (var r = g.R.Next(1, 5); r > 0; r--)
                            md.Append('|').Append(string.Concat(Enumerable.Range(0, cols).Select(_ => $" {g.Words(g.R.Next(1, 6)).Replace("|", "/").Replace("$", "")} |"))).Append('\n');
                        md.Append('\n');
                        tables++;
                        break;
                    }
                    case 3:
                    {
                        var ordered = g.Chance(0.5);
                        var start = g.R.Next(1, 20);
                        for (var i = 0; i < g.R.Next(1, 6); i++)
                            md.Append(ordered ? $"{start + i}. " : "- ").Append(Inline(g, math)).Append('\n');
                        md.Append('\n');
                        break;
                    }
                    case 4: md.Append("> ").Append(Inline(g, math)).Append("\n\n"); break;
                    case 5: md.Append("```\n").Append(g.Words(5)).Append("\n  ").Append(g.Words(3)).Append("\n```\n\n"); break;
                    case 6: md.Append("---\n\n"); break;
                    case 7 when math: md.Append("$$").Append(g.Pick(Latex)).Append("$$\n\n"); display++; break;
                    default: md.Append(Inline(g, math)).Append("\n\n"); break;
                }
            }
        }
        return new GeneratedDocument(md.ToString(), headings, preamble, tables, display);
    }
}
