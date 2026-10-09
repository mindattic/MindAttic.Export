using MindAttic.Export.Tests.Support;
using MindAttic.Export.Text;

namespace MindAttic.Export.Tests.TextTests;

/// <summary>Invariants of the inline parser over thousands of seeded random strings.</summary>
[TestFixture]
public class ProseInlinePropertyTests
{
    private const int Count = 1000;

    /// <summary>Seeded strings: dense marker soup, prose paragraphs, and plain prose.</summary>
    public static string Sample(int seed)
    {
        var g = new TextGen(seed * 7919 + 17);
        return (seed % 3) switch
        {
            0 => g.MarkerSoup(60),
            1 => g.Paragraph(1, 30),
            _ => g.MultiParagraph(3)
        };
    }

    public static IEnumerable<TestCaseData> Seeds(string name) =>
        Enumerable.Range(0, Count).Select(s => new TestCaseData(s).SetName($"{name}(seed {s})"));

    public static IEnumerable<TestCaseData> ConcatSeeds() => Seeds("Concatenated_runs_equal_StripFormatting");
    public static IEnumerable<TestCaseData> PositionSeeds() => Seeds("Every_run_is_the_source_substring_at_its_start");
    public static IEnumerable<TestCaseData> OrderSeeds() => Seeds("Runs_are_nonempty_ordered_and_disjoint");
    public static IEnumerable<TestCaseData> LegacySeeds() => Seeds("Parse_matches_frozen_legacy_parser");
    public static IEnumerable<TestCaseData> StyleSeeds() => Seeds("Parse_never_produces_math_or_code_styles");

    [TestCaseSource(nameof(ConcatSeeds))]
    public void Concat(int seed)
    {
        var s = Sample(seed);
        Assert.That(string.Concat(ProseInline.ParseRuns(s).Select(r => r.Text)), Is.EqualTo(ProseInline.StripFormatting(s)));
        Assert.That(string.Concat(ProseInline.Parse(s).Select(r => r.Text)), Is.EqualTo(ProseInline.StripFormatting(s)));
    }

    [TestCaseSource(nameof(PositionSeeds))]
    public void Positions(int seed)
    {
        var s = Sample(seed);
        foreach (var run in ProseInline.ParseRuns(s))
            Assert.That(s.Substring(run.Start, run.Text.Length), Is.EqualTo(run.Text), $"run at {run.Start}");
    }

    [TestCaseSource(nameof(OrderSeeds))]
    public void Order(int seed)
    {
        var s = Sample(seed);
        var runs = ProseInline.ParseRuns(s);
        var spans = ProseInline.Parse(s);
        Assert.That(spans.Select(x => (x.Text, x.Style)), Is.EqualTo(runs.Select(r => (r.Text, r.Style))), "Parse is ParseRuns without positions");
        var end = 0;
        foreach (var run in runs)
        {
            Assert.That(run.Text, Is.Not.Empty);
            Assert.That(run.Start, Is.GreaterThanOrEqualTo(end));
            end = run.Start + run.Text.Length;
        }
        Assert.That(end, Is.LessThanOrEqualTo(s.Length));
        Assert.That(ProseInline.StripFormatting(s).Length, Is.LessThanOrEqualTo(s.Length));
    }

    [TestCaseSource(nameof(LegacySeeds))]
    public void Legacy(int seed)
    {
        var s = Sample(seed);
        var current = ProseInline.Parse(s).Select(x => (x.Text, (int)x.Style));
        var legacy = ProseLegacy.ProseInline.Parse(s).Select(x => (x.Text, (int)x.Style));
        Assert.That(current, Is.EqualTo(legacy));
        Assert.That(ProseInline.StripFormatting(s), Is.EqualTo(ProseLegacy.ProseInline.StripFormatting(s)));
        Assert.That(ProseInline.ParseRuns(s).Select(r => (r.Start, r.Text, (int)r.Style)),
                    Is.EqualTo(ProseLegacy.ProseInline.ParseRuns(s).Select(r => (r.Start, r.Text, (int)r.Style))));
    }

    [TestCaseSource(nameof(StyleSeeds))]
    public void Styles(int seed)
    {
        const ProseInline.Style parserStyles = ProseInline.Style.Bold | ProseInline.Style.Italic
                                            | ProseInline.Style.Underline | ProseInline.Style.Strikethrough;
        foreach (var span in ProseInline.Parse(Sample(seed)))
            Assert.That(span.Style & ~parserStyles, Is.EqualTo(ProseInline.Style.None));
    }

    // ── marker-free text ─────────────────────────────────────────────────────

    public static IEnumerable<TestCaseData> PlainSeeds() =>
        Enumerable.Range(0, 300).Select(s => new TestCaseData(new TextGen(s, TextFeatures.Unicode | TextFeatures.Emoji | TextFeatures.Controls).MultiParagraph(3))
            .SetName($"StripFormatting_is_identity_without_markers(seed {s})"));

    [TestCaseSource(nameof(PlainSeeds))]
    public void Plain(string text)
    {
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(text));
        Assert.That(ProseInline.StripFormatting(ProseInline.StripFormatting(text)), Is.EqualTo(text));
        var runs = ProseInline.ParseRuns(text);
        if (text.Length > 0)
        {
            Assert.That(runs, Has.Count.EqualTo(1));
            Assert.That(runs[0], Is.EqualTo(new ProseInline.Run(0, text, ProseInline.Style.None)));
        }
    }

    // ── balanced markers ─────────────────────────────────────────────────────

    private static readonly (string Open, string Close, ProseInline.Style Style)[] Markers =
    [
        ("**", "**", ProseInline.Style.Bold),
        ("*", "*", ProseInline.Style.Italic),
        ("~~", "~~", ProseInline.Style.Strikethrough),
        ("<u>", "</u>", ProseInline.Style.Underline)
    ];

    public static IEnumerable<TestCaseData> BalancedSeeds()
    {
        for (var s = 0; s < 200; s++)
            for (var m = 0; m < Markers.Length; m++)
                yield return new TestCaseData(s, m).SetName($"Balanced_marker_styles_its_content(seed {s}, {Markers[m].Style})");
    }

    [TestCaseSource(nameof(BalancedSeeds))]
    public void Balanced(int seed, int marker)
    {
        var g = new TextGen(seed, TextFeatures.Unicode | TextFeatures.Emoji);
        var (open, close, style) = Markers[marker];
        var before = g.Words(g.R.Next(0, 4));
        var inner = g.Words(g.R.Next(1, 5));
        var after = g.Words(g.R.Next(0, 4));
        var text = $"{before} {open}{inner}{close} {after}";
        var spans = ProseInline.Parse(text);
        Assert.That(spans.Where(x => x.Style == style).Select(x => x.Text), Is.EqualTo(new[] { inner }));
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo($"{before} {inner} {after}"));
        var runs = ProseInline.ParseRuns(text);
        var styled = runs.Single(r => r.Style == style);
        Assert.That(styled.Start, Is.EqualTo(before.Length + 1 + open.Length));
    }

    public static IEnumerable<TestCaseData> NestedSeeds()
    {
        for (var s = 0; s < 50; s++)
            for (var a = 0; a < Markers.Length; a++)
                for (var b = 0; b < Markers.Length; b++)
                    if (a != b && !(Markers[a].Open == "*" && Markers[b].Open == "**") && !(Markers[a].Open == "**" && Markers[b].Open == "*"))
                        yield return new TestCaseData(s, a, b).SetName($"Nested_markers_combine_styles(seed {s}, {Markers[a].Style} > {Markers[b].Style})");
    }

    [TestCaseSource(nameof(NestedSeeds))]
    public void Nested(int seed, int outer, int inner)
    {
        var g = new TextGen(seed, TextFeatures.Plain);
        var (o1, c1, s1) = Markers[outer];
        var (o2, c2, s2) = Markers[inner];
        var x = g.Words(2);
        var y = g.Words(2);
        var z = g.Words(2);
        var spans = ProseInline.Parse($"{o1}{x} {o2}{y}{c2} {z}{c1}");
        Assert.That(spans.Select(sp => (sp.Text, sp.Style)), Is.EqualTo(new[]
        {
            (x + " ", s1),
            (y, s1 | s2),
            (" " + z, s1)
        }));
    }

    // ── unmatched markers ────────────────────────────────────────────────────

    [TestCase("*")]
    [TestCase("~~")]
    [TestCase("<u>")]
    [TestCase("</u>")]
    [TestCase("a * b")]
    [TestCase("a ~~ b")]
    [TestCase("x <u> y")]
    [TestCase("x </u> y")]
    [TestCase("~")]
    [TestCase("<u")]
    [TestCase("</")]
    [TestCase("He was *ready")]
    [TestCase("ready* he was")]
    [TestCase("~~ cut")]
    public void Unmatched_markers_are_literal(string text)
    {
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(text));
        Assert.That(ProseInline.Parse(text).All(s => s.Style == ProseInline.Style.None), Is.True);
    }

    [TestCase("**", "**")]
    [TestCase("a ** b", "a ** b")]
    [TestCase("the end**", "the end**")]
    [TestCase("***", "***")]
    public void Unmatched_double_asterisk_is_literal(string text, string expected)
    {
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(expected));
        Assert.That(ProseLegacy.ProseInline.StripFormatting(text), Is.EqualTo(expected));
    }

    [TestCase("**a** *b* ~~c~~ <u>d</u>", "a b c d")]
    [TestCase("***x***", "x")]
    [TestCase("**a *b* c**", "a b c")]
    [TestCase("<u>~~**x**~~</u>", "x")]
    [TestCase("a****b", "ab")]
    [TestCase("<u></u>", "")]
    [TestCase("<U>x</U>", "<U>x</U>")]
    [TestCase("~~~x~~~", "~x~")]
    public void StripFormatting_known_values(string text, string expected)
    {
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(expected));
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(ProseLegacy.ProseInline.StripFormatting(text)));
    }
}
