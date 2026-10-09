using MindAttic.Export.Model;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Support;
using MindAttic.Export.Text;

namespace MindAttic.Export.Tests.TextTests;

[TestFixture]
public class LatexMathTests
{
    /// <summary>Spans rendered compactly: plain text as-is, styled runs as [flags:text] where
    /// i = italic, b = bold, ^ = superscript, _ = subscript.</summary>
    private static string Styled(IEnumerable<ProseInline.Span> spans) =>
        string.Concat(spans.Select(s => s.Style == ProseInline.Style.None ? s.Text : $"[{Flags(s.Style)}:{s.Text}]"));

    private static string Flags(ProseInline.Style s) =>
        (s.HasFlag(ProseInline.Style.Italic) ? "i" : "") + (s.HasFlag(ProseInline.Style.Bold) ? "b" : "")
        + (s.HasFlag(ProseInline.Style.Superscript) ? "^" : "") + (s.HasFlag(ProseInline.Style.Subscript) ? "_" : "");

    [TestCase(@"x", "x", "[i:x]")]
    [TestCase(@"x^2", "x2", "[i:x][^:2]")]
    [TestCase(@"x_i", "xi", "[i:x][i_:i]")]
    [TestCase(@"x_{ij}", "xij", "[i:x][i_:ij]")]
    [TestCase(@"e^{i\pi} + 1 = 0", "eiπ + 1 = 0", "[i:e][i^:i][^:π] + 1 = 0")]
    [TestCase(@"\alpha + \beta", "α + β", "α + β")]
    [TestCase(@"\frac{a}{b}", "a/b", "[i:a]/[i:b]")]
    [TestCase(@"\frac{a+b}{c}", "(a + b)/c", "([i:a] + [i:b])/[i:c]")]
    [TestCase(@"\frac{1}{2}", "1/2", "1/2")]
    [TestCase(@"\sqrt{x}", "√(x)", "√([i:x])")]
    [TestCase(@"\sqrt{x^2 + y^2}", "√(x2 + y2)", "√([i:x][^:2] + [i:y][^:2])")]
    [TestCase(@"\bar{x}", "x̅", "[i:x̅]")]
    [TestCase(@"\overline{AB}", "A̅B̅", "[i:A̅B̅]")]
    [TestCase(@"\hat{p}", "p̂", "[i:p̂]")]
    [TestCase(@"\vec{v}", "v⃗", "[i:v⃗]")]
    [TestCase(@"\dot{x}", "ẋ", "[i:ẋ]")]
    [TestCase(@"\tilde{n}", "ñ", "[i:ñ]")]
    [TestCase(@"\text{GDP}", "GDP", "GDP")]
    [TestCase(@"\mathrm{d}x", "dx", "d[i:x]")]
    [TestCase(@"\mathbf{F} = m\mathbf{a}", "F = ma", "[b:F] = [i:m][b:a]")]
    [TestCase(@"\mathit{val}", "val", "[i:val]")]
    [TestCase(@"\operatorname{argmax}", "argmax", "argmax")]
    [TestCase(@"\ln x", "ln x", "ln [i:x]")]
    [TestCase(@"\max(a, b)", "max(a,b)", "max([i:a],[i:b])")]
    [TestCase(@"\sin\theta", "sin θ", "sin θ")]
    [TestCase(@"\log_2 n", "log2n", "log[_:2][i:n]")]
    [TestCase(@"a - b", "a − b", "[i:a] − [i:b]")]
    [TestCase(@"-x", "−x", "−[i:x]")]
    [TestCase(@"a = -b", "a = −b", "[i:a] = −[i:b]")]
    [TestCase(@"x \le y", "x≤y", "[i:x]≤[i:y]")]
    [TestCase(@"x \geq 0", "x≥0", "[i:x]≥0")]
    [TestCase(@"a \ne b", "a≠b", "[i:a]≠[i:b]")]
    [TestCase(@"\int_0^1 f(x)\,dx", "∫01f(x) dx", "∫[_:0][^:1][i:f]([i:x]) [i:dx]")]
    [TestCase(@"\prod_{k} a_k", "∏kak", "∏[i_:k][i:a][i_:k]")]
    [TestCase(@"\lim_{n \to \infty} a_n", "limn→∞an", "lim[i_:n][_:→∞][i:a][i_:n]")]
    [TestCase(@"\forall x \in S", "∀x∈S", "∀[i:x]∈[i:S]")]
    [TestCase(@"\exists y", "∃y", "∃[i:y]")]
    [TestCase(@"A \cup B \cap C", "A∪B∩C", "[i:A]∪[i:B]∩[i:C]")]
    [TestCase(@"\{1, 2\}", "{1,2}", "{1,2}")]
    [TestCase(@"50\%", "50%", "50%")]
    [TestCase(@"\$5", "$5", "$5")]
    [TestCase(@"\left( x \right)", "(x)", "([i:x])")]
    [TestCase(@"\langle u, v \rangle", "⟨u,v⟩", "⟨[i:u],[i:v]⟩")]
    [TestCase(@"|x|", "|x|", "|[i:x]|")]
    [TestCase(@"\lvert x \rvert", "|x|", "|[i:x]|")]
    [TestCase(@"a \cdot b", "a·b", "[i:a]·[i:b]")]
    [TestCase(@"a \times b", "a×b", "[i:a]×[i:b]")]
    [TestCase(@"\pm 1", "±1", "±1")]
    [TestCase(@"x \approx y", "x≈y", "[i:x]≈[i:y]")]
    [TestCase(@"\partial f", "∂f", "∂[i:f]")]
    [TestCase(@"\nabla \cdot E", "∇·E", "∇·[i:E]")]
    [TestCase(@"\Delta t", "Δt", "Δ[i:t]")]
    [TestCase(@"\Omega", "Ω", "Ω")]
    [TestCase(@"\unknowncmd", "unknowncmd", "unknowncmd")]
    [TestCase(@"\", "", "")]
    [TestCase(@"{", "", "")]
    [TestCase(@"}", "", "")]
    [TestCase(@"^", "", "")]
    [TestCase(@"_", "", "")]
    [TestCase(@"x^", "x", "[i:x]")]
    [TestCase(@"x_", "x", "[i:x]")]
    [TestCase(@"\frac", "/", "/")]
    [TestCase(@"\frac{a}", "a/", "[i:a]/")]
    [TestCase(@"\sqrt", "√()", "√()")]
    [TestCase(@"\bar", "", "")]
    [TestCase(@"\text", "", "")]
    [TestCase(@"", "", "")]
    [TestCase(@"   ", "", "")]
    [TestCase(@"a  b", "ab", "[i:ab]")]
    [TestCase(@"x'", "x'", "[i:x]'")]
    [TestCase(@"f(x) = 3x + 2", "f(x) = 3x + 2", "[i:f]([i:x]) = 3[i:x] + 2")]
    [TestCase(@"V_{\text{max}}", "Vmax", "[i:V][_:max]")]
    [TestCase(@"E = mc^2", "E = mc2", "[i:E] = [i:mc][^:2]")]
    [TestCase(@"P(A|B)", "P(A|B)", "[i:P]([i:A]|[i:B])")]
    [TestCase(@"x_1, x_2, \ldots, x_n", "x1,x2,…,xn", "[i:x][_:1],[i:x][_:2],…,[i:x][i_:n]")]
    [TestCase(@"\cdots", "⋯", "⋯")]
    [TestCase(@"a \to b", "a→b", "[i:a]→[i:b]")]
    [TestCase(@"\Rightarrow", "⇒", "⇒")]
    [TestCase(@"\mapsto", "↦", "↦")]
    [TestCase(@"\neg p \land q \lor r", "¬p∧q∨r", "¬[i:p]∧[i:q]∨[i:r]")]
    [TestCase(@"\emptyset", "∅", "∅")]
    [TestCase(@"2^{10}", "210", "2[^:10]")]
    [TestCase(@"x^{2}_{i}", "x2i", "[i:x][^:2][i_:i]")]
    [TestCase(@"\quad x \qquad y", "x  y", "[i:x]  [i:y]")]
    [TestCase(@"a\,b\;c\!d", "a b cd", "[i:a] [i:b] [i:cd]")]
    [TestCase(@"\displaystyle \sum x", "∑x", "∑[i:x]")]
    [TestCase(@"\big( x \big)", "(x)", "([i:x])")]
    [TestCase(@"\vert x \vert", "|x|", "|[i:x]|")]
    [TestCase(@"a \mid b", "a∣b", "[i:a]∣[i:b]")]
    [TestCase(@"\prime", "′", "′")]
    [TestCase(@"f'(x)", "f'(x)", "[i:f]'([i:x])")]
    [TestCase(@"\circ", "∘", "∘")]
    [TestCase(@"\equiv", "≡", "≡")]
    [TestCase(@"\sim", "∼", "∼")]
    [TestCase(@"\propto", "∝", "∝")]
    [TestCase(@"\subset \subseteq \supset", "⊂⊆⊃", "⊂⊆⊃")]
    [TestCase(@"\notin", "∉", "∉")]
    [TestCase(@"\infty", "∞", "∞")]
    [TestCase(@"\pi r^2", "πr2", "π[i:r][^:2]")]
    [TestCase(@"\tfrac{1}{2}", "1/2", "1/2")]
    [TestCase(@"\dfrac{x}{y}", "x/y", "[i:x]/[i:y]")]
    [TestCase(@"\frac{\alpha}{\beta}", "α/β", "α/β")]
    [TestCase(@"x^{y^z}", "xyz", "[i:x][i^:yz]")]
    [TestCase(@"\exp(x)", "exp(x)", "exp([i:x])")]
    [TestCase(@"\det A", "det A", "det [i:A]")]
    [TestCase(@"\min_x f", "minxf", "min[i_:x][i:f]")]
    [TestCase(@"\cos^2 x", "cos2x", "cos[^:2][i:x]")]
    [TestCase(@"\tan x", "tan x", "tan [i:x]")]
    [TestCase(@"\textit{word}", "word", "[i:word]")]
    [TestCase(@"\textbf{bold}", "bold", "[b:bold]")]
    [TestCase(@"\textrm{roman}", "roman", "roman")]
    [TestCase(@"x < y > z", "x < y > z", "[i:x] < [i:y] > [i:z]")]
    public void Converts(string latex, string plain, string styled)
    {
        var spans = LatexMath.ToSpans(latex);
        Assert.That(LatexMath.ToPlainText(latex), Is.EqualTo(plain));
        Assert.That(Styled(spans), Is.EqualTo(styled));
        Assert.That(string.Concat(spans.Select(s => s.Text)), Is.EqualTo(plain));
    }

    [TestCase(@"x^{n+1}", "n+1")]
    [TestCase(@"10^{-3}", "−3")]
    [TestCase(@"e^{-x}", "−x")]
    [TestCase(@"\sum_{i=1}^{n}", "i=1")]
    public void Operators_inside_scripts_are_not_padded(string latex, string script)
    {
        var scripts = LatexMath.ToSpans(latex)
            .Where(s => s.Style.HasFlag(ProseInline.Style.Superscript) || s.Style.HasFlag(ProseInline.Style.Subscript));
        Assert.That(string.Concat(scripts.Select(s => s.Text)), Does.Contain(script));
    }

    [Test]
    public void Txt_renders_negative_exponent_as_unicode_superscript()
    {
        var manuscript = new Manuscript
        {
            Title = "T",
            Chapters = [new Chapter("C", [new MathBlock(@"10^{-3}", LatexMath.ToSpans(@"10^{-3}"))])]
        };
        Assert.That(TextRenderer.Render(manuscript), Does.Contain("10⁻³"));
    }

    // Inside a sub/superscript operators are compact and a minus is the unary sign (fixed in 3.0.0;
    // these cases previously pinned the padded output).
    [TestCase(@"x^{n+1}", "xn+1")]
    [TestCase(@"10^{-3}", "10−3")]
    [TestCase(@"\sum_{i=1}^{n} x_i", "∑i=1nxi")]
    [TestCase(@"\alpha_{t+1} = \alpha_t - \eta \nabla L", "αt+1 = αt − η∇L")]
    public void Operators_inside_scripts_are_compact(string latex, string plain) =>
        Assert.That(LatexMath.ToPlainText(latex), Is.EqualTo(plain));

    // ── styling invariants ───────────────────────────────────────────────────

    [TestCase("a", true)]
    [TestCase("Z", true)]
    [TestCase("1", false)]
    [TestCase("+", false)]
    public void Single_ascii_letters_are_italic_variables(string latex, bool italic) =>
        Assert.That(LatexMath.ToSpans(latex).Single().Style.HasFlag(ProseInline.Style.Italic), Is.EqualTo(italic));

    public static IEnumerable<TestCaseData> GreekNames() =>
        new[] { "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta", "iota", "kappa", "lambda", "mu", "nu", "xi",
                "pi", "rho", "sigma", "tau", "upsilon", "phi", "chi", "psi", "omega", "Gamma", "Delta", "Theta", "Lambda", "Xi",
                "Pi", "Sigma", "Upsilon", "Phi", "Psi", "Omega" }
            .Select(n => new TestCaseData(n).SetName($"Greek_letter_is_one_upright_char({n})"));

    [TestCaseSource(nameof(GreekNames))]
    public void Greek_letter_is_one_upright_char(string name)
    {
        var spans = LatexMath.ToSpans("\\" + name);
        Assert.That(spans, Has.Count.EqualTo(1));
        Assert.That(spans[0].Text, Has.Length.EqualTo(1));
        Assert.That(spans[0].Style, Is.EqualTo(ProseInline.Style.None));
        Assert.That(char.IsLetter(spans[0].Text[0]), Is.True);
        Assert.That(spans[0].Text[0], Is.GreaterThan('Ͱ'));
    }

    [TestCase("x^2", "2", ProseInline.Style.Superscript)]
    [TestCase("x_2", "2", ProseInline.Style.Subscript)]
    [TestCase("x^{22}", "22", ProseInline.Style.Superscript)]
    [TestCase("x_{10}", "10", ProseInline.Style.Subscript)]
    [TestCase("\\alpha^2", "2", ProseInline.Style.Superscript)]
    [TestCase("y_0^2", "0", ProseInline.Style.Subscript)]
    public void Scripts_carry_their_style(string latex, string text, ProseInline.Style style) =>
        Assert.That(LatexMath.ToSpans(latex).Any(s => s.Text == text && s.Style == style), Is.True, Styled(LatexMath.ToSpans(latex)));

    // ── robustness ───────────────────────────────────────────────────────────

    private static readonly string[] Tokens =
    [
        "\\frac", "\\sqrt", "\\bar", "\\hat", "\\vec", "\\text", "\\mathbf", "\\alpha", "\\sum", "\\int", "\\left", "\\right",
        "\\", "\\\\", "{", "}", "{{", "}}", "^", "_", "^{", "_{", "x", "y", "2", "-", "+", "=", "<", ">", " ", "  ", "(", ")",
        "\\,", "\\;", "\\!", "\\%", "\\$", "\\{", "\\}", "\\unknown", "é", "🙂", "\\ln", "\\max", "|", ",", "'", "\t", "\n", "\\le"
    ];

    public static IEnumerable<TestCaseData> RandomLatex() =>
        Enumerable.Range(0, 2500).Select(s => new TestCaseData(s).SetName($"Never_throws_on_random_latex(seed {s})"));

    [TestCaseSource(nameof(RandomLatex))]
    public void Never_throws_on_random_latex(int seed)
    {
        var r = new Random(seed);
        var latex = string.Concat(Enumerable.Range(0, r.Next(0, 25)).Select(_ => Tokens[r.Next(Tokens.Length)]));
        List<ProseInline.Span> spans = [];
        Assert.DoesNotThrow(() => spans = LatexMath.ToSpans(latex), latex);
        Assert.That(spans.All(s => s.Text.Length > 0), Is.True, "no empty spans");
        var plain = string.Concat(spans.Select(s => s.Text));
        Assert.That(LatexMath.ToPlainText(latex), Is.EqualTo(plain));
        Assert.That(plain, Does.Not.Contain("  "), $"no doubled ASCII space: {latex}");
        Assert.That(spans.Any(s => s.Style.HasFlag(ProseInline.Style.Code) || s.Style.HasFlag(ProseInline.Style.Underline)
                                   || s.Style.HasFlag(ProseInline.Style.Strikethrough)), Is.False);
    }

    [Test]
    public void Deeply_nested_groups_do_not_overflow()
    {
        var latex = string.Concat(Enumerable.Repeat("x^{", 200)) + "y" + new string('}', 200);
        Assert.DoesNotThrow(() => LatexMath.ToSpans(latex));
        var unbalanced = string.Concat(Enumerable.Repeat("\\frac{", 300));
        Assert.DoesNotThrow(() => LatexMath.ToSpans(unbalanced));
    }
}
