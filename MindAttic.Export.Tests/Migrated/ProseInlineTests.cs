using MindAttic.Export.Text;

namespace MindAttic.Export.Tests.Migrated;

/// <summary>
/// Migrated from Prose: src/Prose.UnitTests/ProseInlineTests.cs (class ProseInlineTests, Prose
/// commit e6dc3c37e), retargeted from Prose.Core.Services.ProseInline to
/// MindAttic.Export.Text.ProseInline. Intent and expected values unchanged. (The same file's
/// BeatMarkupValidateTests cover Prose entity markup, which stays in Prose, and are not migrated.)
///
/// The inline-emphasis parser sits on the prose read AND write path — the editor renders through
/// it and every exporter formats through it — so a bug here is a bug in the manuscript.
/// </summary>
[TestFixture]
public class ProseInlineTests
{
    private static string Render(string text) =>
        string.Concat(ProseInline.Parse(text).Select(s =>
            s.Style == ProseInline.Style.None ? s.Text : $"[{s.Style}:{s.Text}]"));

    // Origin: ProseInlineTests.Italic_SingleAsterisks_IsItalic
    [Test]
    public void Italic_SingleAsterisks_IsItalic()
    {
        Assert.That(Render("He was *ready* for it."), Is.EqualTo("He was [Italic:ready] for it."));
    }

    // Origin: ProseInlineTests.Bold_DoubleAsterisks_IsBoldNotTwoItalics
    [Test]
    public void Bold_DoubleAsterisks_IsBoldNotTwoItalics()
    {
        // The regression this parser was written for: the old exporter split on a single '*', so
        // "**TO: STANDING CONTRACT**" exported as ordinary body text.
        Assert.That(Render("The screen read **TO: STANDING CONTRACT** and nothing else."),
                    Is.EqualTo("The screen read [Bold:TO: STANDING CONTRACT] and nothing else."));
    }

    // Origin: ProseInlineTests.LoneAsterisk_BeforeLaterBold_StaysLiteral
    [Test]
    public void LoneAsterisk_BeforeLaterBold_StaysLiteral()
    {
        // The first half of a later "**" is not a closing italic marker.
        Assert.That(Render("5 * 3 = 15. **NOTE**"), Is.EqualTo("5 * 3 = 15. [Bold:NOTE]"));
    }

    // Origin: ProseInlineTests.Strikethrough_AndUnderline_AreParsed
    [Test]
    public void Strikethrough_AndUnderline_AreParsed()
    {
        Assert.That(Render("~~redacted~~"), Is.EqualTo("[Strikethrough:redacted]"));
        Assert.That(Render("<u>signed</u>"), Is.EqualTo("[Underline:signed]"));
    }

    // Origin: ProseInlineTests.Nested_BoldInsideItalic_CarriesBothStyles
    [Test]
    public void Nested_BoldInsideItalic_CarriesBothStyles()
    {
        Assert.That(Render("*he read **NOW** aloud*"),
                    Is.EqualTo("[Italic:he read ][Italic, Bold:NOW][Italic: aloud]"));
    }

    // Origin: ProseInlineTests.UnmatchedMarker_IsLeftAsLiteralText
    [Test]
    public void UnmatchedMarker_IsLeftAsLiteralText()
    {
        // A half-typed emphasis must not swallow the rest of the paragraph — the author needs to
        // see that it is wrong, and prose contains real asterisks.
        Assert.That(Render("A lone * in the line."), Is.EqualTo("A lone * in the line."));
        Assert.That(Render("He was *ready"), Is.EqualTo("He was *ready"));
    }

    // Origin: ProseInlineTests.StripFormatting_RemovesEveryMarker
    [Test]
    public void StripFormatting_RemovesEveryMarker()
    {
        const string text = "He was *ready*, the screen said **GO**, the rest was ~~cut~~ and <u>signed</u>.";
        Assert.That(ProseInline.StripFormatting(text),
                    Is.EqualTo("He was ready, the screen said GO, the rest was cut and signed."));
    }

    // Origin: ProseInlineTests.StripFormatting_LeavesPlainProseUntouched
    [Test]
    public void StripFormatting_LeavesPlainProseUntouched()
    {
        const string text = "The apartment smelled like camphor and fear-sweat.";
        Assert.That(ProseInline.StripFormatting(text), Is.EqualTo(text));
    }

    // Origin: ProseInlineTests.Parse_EmptyAndNull_AreSafe
    [Test]
    public void Parse_EmptyAndNull_AreSafe()
    {
        Assert.That(ProseInline.Parse(null), Is.Empty);
        Assert.That(ProseInline.Parse(""), Is.Empty);
        Assert.That(ProseInline.StripFormatting(null), Is.EqualTo(""));
    }
}
