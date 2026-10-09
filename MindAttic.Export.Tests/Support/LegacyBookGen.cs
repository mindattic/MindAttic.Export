using ProseLegacy;

namespace MindAttic.Export.Tests.Support;

/// <summary>How far a generated book may stray from what BookSpineService can produce.</summary>
public sealed record BookGenOptions
{
    /// <summary>Allow null/empty/whitespace chapter headings (including an untitled "preamble"
    /// first chapter). BookSpineService always resolves a non-empty heading, but LoadAsync (epub,
    /// pdf, txt) normalises blank ones itself, so those formats are exercised with them.</summary>
    public bool BlankHeadings { get; init; }

    public TextFeatures Features { get; init; } = TextFeatures.All;

    public int MaxChapters { get; init; } = 12;

    public static BookGenOptions Spine { get; } = new();
    public static BookGenOptions LoadAsync { get; } = new() { BlankHeadings = true };
}

/// <summary>Seeded random Prose books in the shape the legacy exporters read.</summary>
public static class LegacyBookGen
{
    private static readonly string[] TenetTexts =
    [
        "礼\nRei\nRespect", "義\nGi\nRighteousness", "勇\nYū\nCourage", "仁\nJin\nCompassion",
        "誠\nMakoto\nHonesty", "名誉\n\nMeiyo\n  Honour  ", "忠義", "  礼  \r\n  Rei  \r\n"
    ];

    public static LegacyBook Generate(int seed, BookGenOptions? options = null)
    {
        options ??= BookGenOptions.Spine;
        var g = new TextGen(seed, options.Features);
        var chapterCount = g.Chance(0.2) ? 1 : g.R.Next(2, options.MaxChapters + 1);

        var chapters = new List<LegacyChapter>();
        for (var c = 0; c < chapterCount; c++)
        {
            var beats = new List<LegacyBeat>();
            var beatCount = g.R.Next(1, 8);
            for (var b = 0; b < beatCount; b++)
            {
                var roll = g.R.NextDouble();
                string? sub = null;
                if (b > 0 && g.Chance(0.15))
                    sub = g.Chance(0.2) ? $"  {g.Title(4)} " : g.Title(4);

                if (roll < 0.08 || (b == beatCount - 1 && g.Chance(0.06)))
                {
                    beats.Add(new LegacyBeat(g.NewGuid(), g.Pick(TenetTexts), sub, g.Chance(0.5) ? "tenet" : "Tenet"));
                }
                else if (roll < 0.12 && !(b > 0 && beats[^1].Kind is not null))
                {
                    // An empty beat — never straight after a tenet (see LegacyEquivalenceTests).
                    beats.Add(new LegacyBeat(g.NewGuid(), g.Pick(["", "   ", "\n", null!]), sub));
                }
                else
                {
                    beats.Add(new LegacyBeat(g.NewGuid(), g.MultiParagraph(), sub, g.Chance(0.1) ? "prose" : null));
                }
            }
            chapters.Add(new LegacyChapter(Heading(g, c, options), beats));
        }

        var glossary = new List<LegacyGlossaryTerm>();
        if (g.Chance(0.6))
        {
            var n = g.R.Next(1, 9);
            for (var i = 0; i < n; i++)
                glossary.Add(new LegacyGlossaryTerm(
                    g.Title(2),
                    g.R.Next(4) switch { 0 => null, 1 => "", 2 => "  ", _ => g.Title(4) },
                    g.Paragraph(3, 25)));
            glossary.Sort((a, b) => string.CompareOrdinal(a.Term, b.Term));
        }

        return new LegacyBook
        {
            Title = g.Chance(0.1) ? $" {g.Title()} " : g.Title(),
            Subtitle = g.R.Next(5) switch { 0 => null, 1 => "", 2 => "   ", 3 => $" {g.Title()} ", _ => g.Title() },
            Author = g.R.Next(5) switch { 0 => null, 1 => "", 2 => " ", 3 => "  Ryan  DeBraal ", _ => g.Title(3) },
            Description = g.R.Next(4) switch { 0 => null, 1 => "", 2 => "  ", _ => g.Paragraph(5, 60) },
            Slug = $"book-{seed}",
            Chapters = chapters,
            Glossary = glossary,
            IncludeToc = g.Chance(0.5)
        };
    }

    private static string? Heading(TextGen g, int index, BookGenOptions options)
    {
        if (options.BlankHeadings && g.Chance(index == 0 ? 0.3 : 0.12))
            return g.Pick<string?>([null, "", "   ", "\t"]);
        return g.R.Next(6) switch
        {
            0 => $"Chapter {index + 1}",
            1 => $"Chapter {index + 1} — {g.Title(4)}",
            2 => $"Interlude: {g.Title(2)}",
            3 => g.Title(5),
            4 => $"Part {index + 1}: {g.Words(2)} & <{g.Words(1)}> \"{g.Words(1)}\"",
            _ => $"**{g.Title(2)}** *{g.Title(1)}*"
        };
    }
}
