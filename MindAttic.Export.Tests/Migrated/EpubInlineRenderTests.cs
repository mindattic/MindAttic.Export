using MindAttic.Export.Renderers;

namespace MindAttic.Export.Tests.Migrated;

/// <summary>
/// Migrated from Prose: src/Prose.UnitTests/SplitAndInlineRenderTests.cs (Prose commit e6dc3c37e),
/// the one pure-renderer assertion that maps onto this library:
/// ManuscriptExportService.EpubRenderInline → EpubRenderer.RenderInline.
///
/// Not migrated (no counterpart here — they test Prose services that stay in Prose): the
/// beat-split/entity-tag tests (NodeWorkbenchService), mojibake repair, quote grounding, universe
/// graph properties, beat-mode detection, BookExportService.ToWellFormedXhtml, reflow and SQL seed
/// batching. ExportServiceMarkdownTagBoundaryTests (ExportService.ToMarkdown, an HTML→Markdown
/// converter) has no counterpart either: this library reads Markdown, it does not convert HTML.
/// </summary>
[TestFixture]
public class EpubInlineRenderTests
{
    // Origin: SplitAndInlineRenderTests.The_epub_renders_bold_italic_underline_and_strike_and_keeps_a_stray_asterisk
    [Test]
    public void The_epub_renders_bold_italic_underline_and_strike_and_keeps_a_stray_asterisk()
    {
        Assert.That(EpubRenderer.RenderInline("**SCREEN** and *soft*"),
            Is.EqualTo("<strong>SCREEN</strong> and <em>soft</em>"));
        Assert.That(EpubRenderer.RenderInline("<u>x</u> ~~y~~"), Is.EqualTo("<u>x</u> <s>y</s>"));
        Assert.That(EpubRenderer.RenderInline("f***ing hell"), Does.Not.Contain("<em>ing hell"));
        Assert.That(EpubRenderer.RenderInline("a < b & c"), Is.EqualTo("a &lt; b &amp; c"));
    }
}
