using System.Text.RegularExpressions;

namespace ProseLegacy;

// The in-memory stand-in for what Prose's legacy exporters read from the database:
//   * LegacyBook      ≈ the Node row (Title, Subtitle, Author, Description, Slug) + settings.DocxIncludeToc
//   * LegacyChapter   ≈ BookSpineService.SpineChapter (Heading + its beats in reading order)
//   * LegacyBeat      ≈ Beat (Id, Text, Kind) joined with SpineBeat.IsSubHeading/Title
//   * LegacyGlossary  ≈ GlossaryService.GetUsedTermsAsync (already sorted)
// Flattening Chapters[*].Beats gives NodeWorkbenchService.GetOrderedBeatsAsync's `ordered` list.

/// <summary>One beat. <see cref="SubHeadingTitle"/> non-null means SpineBeat.IsSubHeading with
/// that (untrimmed) Beat.Title; the spine never sets it on a chapter's opening beat.</summary>
public sealed record LegacyBeat(Guid Id, string? Text, string? SubHeadingTitle = null, string? Kind = null)
{
    public bool IsSubHeading => SubHeadingTitle is not null;
}

public sealed record LegacyChapter(string? Heading, List<LegacyBeat> Beats);

public sealed record LegacyGlossaryTerm(string Term, string? FullForm, string Definition);

public sealed record LegacyBook
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string Slug { get; init; } = "slug";
    public List<LegacyChapter> Chapters { get; init; } = [];
    public List<LegacyGlossaryTerm> Glossary { get; init; } = [];

    /// <summary>settings.DocxIncludeToc.</summary>
    public bool IncludeToc { get; init; }

    /// <summary>spine.ChapterCount.</summary>
    public int ChapterCount => Chapters.Count;

    /// <summary>workbench.GetOrderedBeatsAsync.</summary>
    public List<LegacyBeat> Ordered => Chapters.SelectMany(c => c.Beats).ToList();

    public override string ToString() =>
        $"'{Title}' ch={Chapters.Count} beats={Ordered.Count} gloss={Glossary.Count} toc={IncludeToc}";
}

/// <summary>Verbatim copy of the one pure helper the legacy exporters called from
/// <c>Prose.Core.Services.BeatMarkup</c>.</summary>
public static class LegacyBeatMarkup
{
    private static readonly Regex EntityTagPattern =
        new(@"<entity\b[^>]*>(.*?)</entity>", RegexOptions.Compiled | RegexOptions.Singleline);

    public static string StripEntityTags(string? text) =>
        string.IsNullOrEmpty(text) ? text ?? "" : EntityTagPattern.Replace(text, "$1");
}

/// <summary>Seams for the values the legacy code took from the clock and the RNG.</summary>
public sealed record LegacySeams(Guid BookUuid, DateTime UtcNow)
{
    public static LegacySeams Fixed { get; } =
        new(Guid.Parse("0f8a3a3c-1d2e-4b5f-9a6b-7c8d9e0f1a2b"), new DateTime(2026, 10, 4, 19, 21, 3, DateTimeKind.Utc));
}
