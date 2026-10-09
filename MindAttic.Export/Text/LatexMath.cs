using System.Text;

namespace MindAttic.Export.Text;

/// <summary>
/// Converts the small subset of LaTeX used in prose documents (Greek letters, sub/superscripts,
/// fractions, overlines, common operators and symbols) into styled spans: Unicode text with
/// <see cref="ProseInline.Style.Superscript"/> and <see cref="ProseInline.Style.Subscript"/> runs
/// and italic single-letter variables. It is a typesetting convenience, not a TeX engine; any
/// unknown command is written as its bare name.
/// </summary>
public static class LatexMath
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ε",
        ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η", ["theta"] = "θ", ["vartheta"] = "ϑ",
        ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ",
        ["pi"] = "π", ["varpi"] = "ϖ", ["rho"] = "ρ", ["varrho"] = "ϱ", ["sigma"] = "σ", ["varsigma"] = "ς",
        ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "φ", ["varphi"] = "φ", ["chi"] = "χ", ["psi"] = "ψ",
        ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ",
        ["Pi"] = "Π", ["Sigma"] = "Σ", ["Upsilon"] = "Υ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
        ["partial"] = "∂", ["nabla"] = "∇", ["infty"] = "∞", ["in"] = "∈", ["notin"] = "∉",
        ["subset"] = "⊂", ["subseteq"] = "⊆", ["supset"] = "⊃", ["cup"] = "∪", ["cap"] = "∩",
        ["bigcup"] = "⋃", ["bigcap"] = "⋂", ["sum"] = "∑", ["prod"] = "∏", ["int"] = "∫",
        ["cdot"] = "·", ["times"] = "×", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓",
        ["le"] = "≤", ["leq"] = "≤", ["ge"] = "≥", ["geq"] = "≥", ["ne"] = "≠", ["neq"] = "≠",
        ["approx"] = "≈", ["equiv"] = "≡", ["sim"] = "∼", ["propto"] = "∝", ["to"] = "→",
        ["rightarrow"] = "→", ["leftarrow"] = "←", ["Rightarrow"] = "⇒", ["Leftarrow"] = "⇐",
        ["leftrightarrow"] = "↔", ["Leftrightarrow"] = "⇔", ["mapsto"] = "↦", ["forall"] = "∀",
        ["exists"] = "∃", ["neg"] = "¬", ["land"] = "∧", ["lor"] = "∨", ["emptyset"] = "∅",
        ["ldots"] = "…", ["cdots"] = "⋯", ["dots"] = "…", ["prime"] = "′", ["circ"] = "∘",
        ["quad"] = " ", ["qquad"] = "  ", [","] = " ", [";"] = " ",
        [" "] = " ", ["!"] = "", ["{"] = "{", ["}"] = "}", ["%"] = "%", ["$"] = "$", ["_"] = "_",
        ["ln"] = "ln", ["log"] = "log", ["exp"] = "exp", ["max"] = "max", ["min"] = "min",
        ["sin"] = "sin", ["cos"] = "cos", ["tan"] = "tan", ["lim"] = "lim", ["det"] = "det",
        ["left"] = "", ["right"] = "", ["big"] = "", ["Big"] = "", ["displaystyle"] = "",
        ["mid"] = "∣", ["vert"] = "|", ["lvert"] = "|", ["rvert"] = "|", ["langle"] = "⟨", ["rangle"] = "⟩"
    };

    /// <summary>Words written upright rather than as italic variables.</summary>
    private static readonly HashSet<string> Upright = ["ln", "log", "exp", "max", "min", "sin", "cos", "tan", "lim", "det"];

    public static List<ProseInline.Span> ToSpans(string latex)
    {
        var builder = new SpanBuilder();
        Convert(latex.Trim(), builder, ProseInline.Style.None);
        return builder.Build();
    }

    /// <summary>Plain Unicode approximation (super/subscripts written with ^ and _ markers).</summary>
    public static string ToPlainText(string latex) => string.Concat(ToSpans(latex).Select(s => s.Text));

    private static void Convert(string s, SpanBuilder outp, ProseInline.Style style)
    {
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c is '_' or '^')
            {
                var (body, next) = Group(s, i + 1);
                var flag = c == '_' ? ProseInline.Style.Subscript : ProseInline.Style.Superscript;
                Convert(body, outp, style | flag);
                i = next;
            }
            else if (c == '\\')
            {
                var (name, next) = Command(s, i);
                i = next;
                if (name is "frac" or "tfrac" or "dfrac")
                {
                    var (num, n1) = Group(s, i);
                    var (den, n2) = Group(s, n1);
                    i = n2;
                    WrapIfComplex(num, outp, style);
                    outp.Add("/", style);
                    WrapIfComplex(den, outp, style);
                }
                else if (name is "bar" or "overline" or "hat" or "tilde" or "vec" or "dot")
                {
                    var (arg, n1) = Group(s, SkipSpace(s, i));
                    i = n1;
                    var mark = name switch { "hat" => '̂', "tilde" => '̃', "vec" => '⃗', "dot" => '̇', _ => '̅' };
                    var plain = new SpanBuilder();
                    Convert(arg, plain, style);
                    foreach (var span in plain.Build())
                    {
                        var sb = new StringBuilder();
                        foreach (var ch in span.Text) { sb.Append(ch); if (!char.IsWhiteSpace(ch)) sb.Append(mark); }
                        outp.Add(sb.ToString(), span.Style);
                    }
                }
                else if (name is "text" or "mathrm" or "textrm" or "operatorname" or "mathit" or "textit" or "mathbf" or "textbf")
                {
                    var (arg, n1) = Group(s, i);
                    i = n1;
                    var extra = name switch
                    {
                        "mathit" or "textit" => ProseInline.Style.Italic,
                        "mathbf" or "textbf" => ProseInline.Style.Bold,
                        _ => ProseInline.Style.None
                    };
                    outp.Add(arg, (style & ~ProseInline.Style.Italic) | extra);
                }
                else if (name is "sqrt")
                {
                    var (arg, n1) = Group(s, i);
                    i = n1;
                    outp.Add("√(", style);
                    Convert(arg, outp, style);
                    outp.Add(")", style);
                }
                else if (Symbols.TryGetValue(name, out var sym))
                {
                    outp.Add(sym, style);
                    // Operator names (ln, max, …) are separated from a following operand.
                    if (Upright.Contains(name))
                    {
                        var k = SkipSpace(s, i);
                        if (k < s.Length && (char.IsLetterOrDigit(s[k]) || s[k] == '\\')) outp.Add(" ", style);
                    }
                }
                else
                {
                    outp.Add(name, style);
                }
            }
            else if (c is '{' or '}')
            {
                i++;
            }
            else if (char.IsAsciiLetter(c))
            {
                outp.Add(c.ToString(), style | ProseInline.Style.Italic);
                i++;
            }
            else if (c is '=' or '+' or '<' or '>')
            {
                outp.Add($" {c} ", style);
                i++;
            }
            else if (c == '-')
            {
                // Binary minus between operands; unary after an operator or at the start.
                var unary = outp.LastNonSpaceIsOperatorOrEmpty();
                outp.Add(unary ? "−" : " − ", style);
                i++;
            }
            else if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else
            {
                outp.Add(c.ToString(), style);
                i++;
            }
        }
    }

    private static void WrapIfComplex(string body, SpanBuilder outp, ProseInline.Style style)
    {
        var inner = new SpanBuilder();
        Convert(body, inner, style);
        var spans = inner.Build();
        var text = string.Concat(spans.Select(x => x.Text));
        var simple = !text.Any(ch => ch is ' ' or '+' or '−' or '-' or '=' or '/' or ' ');
        if (!simple) outp.Add("(", style);
        foreach (var span in spans) outp.Add(span.Text, span.Style);
        if (!simple) outp.Add(")", style);
    }

    private static int SkipSpace(string s, int i)
    {
        while (i < s.Length && s[i] == ' ') i++;
        return i;
    }

    private static (string Name, int Next) Command(string s, int i)
    {
        var j = i + 1;
        if (j >= s.Length) return ("", j);
        if (!char.IsAsciiLetter(s[j])) return (s[j].ToString(), j + 1);
        while (j < s.Length && char.IsAsciiLetter(s[j])) j++;
        var name = s[(i + 1)..j];
        return (Upright.Contains(name) ? name : name, j);
    }

    /// <summary>A {…} group or a single token at s[i].</summary>
    private static (string Body, int Next) Group(string s, int i)
    {
        i = SkipSpace(s, i);
        if (i >= s.Length) return ("", i);
        if (s[i] == '{')
        {
            int depth = 1, j = i + 1;
            while (j < s.Length && depth > 0)
            {
                if (s[j] == '{') depth++;
                else if (s[j] == '}') depth--;
                j++;
            }
            return (s[(i + 1)..Math.Max(i + 1, j - 1)], j);
        }
        if (s[i] == '\\')
        {
            var (_, next) = Command(s, i);
            return (s[i..next], next);
        }
        return (s[i].ToString(), i + 1);
    }

    private sealed class SpanBuilder
    {
        private readonly List<ProseInline.Span> spans = [];

        public void Add(string text, ProseInline.Style style)
        {
            if (text.Length == 0) return;
            if (spans.Count > 0 && spans[^1].Style == style)
                spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            else
                spans.Add(new ProseInline.Span(text, style));
        }

        public bool LastNonSpaceIsOperatorOrEmpty()
        {
            for (var k = spans.Count - 1; k >= 0; k--)
            {
                var t = spans[k].Text.TrimEnd();
                if (t.Length == 0) continue;
                return t[^1] is '=' or '+' or '−' or '<' or '>' or '(' or ',' or '≤' or '≥' or '∈';
            }
            return true;
        }

        public List<ProseInline.Span> Build()
        {
            // Collapse doubled spaces created by operator padding.
            var result = new List<ProseInline.Span>();
            foreach (var span in spans)
            {
                var text = span.Text;
                while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
                if (result.Count > 0 && result[^1].Text.EndsWith(' ') && text.StartsWith(' ')) text = text[1..];
                if (text.Length > 0) result.Add(span with { Text = text });
            }
            if (result.Count > 0)
            {
                result[0] = result[0] with { Text = result[0].Text.TrimStart() };
                result[^1] = result[^1] with { Text = result[^1].Text.TrimEnd() };
            }
            return result.Where(r => r.Text.Length > 0).ToList();
        }
    }
}
