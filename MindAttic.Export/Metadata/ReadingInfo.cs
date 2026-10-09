using System.Text.RegularExpressions;

namespace MindAttic.Export.Metadata;

/// <summary>
/// The catalog sidecars of an export bundle: <c>description.txt</c> with its reading-time line,
/// and <c>keywords.txt</c>. Migrated from <c>Prose.Core.Services.NodeFullExportService</c>.
///
/// <para>Kindle page count is words / 250 (the commonly cited convention for Amazon's Kindle
/// page display) and reading time is words / 200 (average adult silent-reading speed).</para>
/// </summary>
public static class ReadingInfo
{
    public const string DescriptionFileName = "description.txt";
    public const string KeywordsFileName = "keywords.txt";

    public static int CountWords(string text) =>
        text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

    public static int KindlePages(int wordCount) => Math.Max(1, (int)Math.Round(wordCount / 250.0));

    public static int ReadingMinutes(int wordCount) => Math.Max(1, (int)Math.Round(wordCount / 200.0));

    public static string FormatReadingTime(int totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        if (hours > 0 && minutes > 0) return $"{hours} hr {minutes} min";
        if (hours > 0) return $"{hours} hr";
        return $"{minutes} min";
    }

    public static string ReadingLine(int wordCount) =>
        $"Approximately {KindlePages(wordCount)} pages and {FormatReadingTime(ReadingMinutes(wordCount))} to read.";

    private static readonly Regex ReadingInfoLineRx =
        new(@"\n*\s*Approximately\s+\d+\s+pages?\s+and\s+.+?\s+to\s+read\.\s*$",
            RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

    /// <summary>Removes a previously-appended "Approximately N pages and X to read." trailing
    /// line so re-exporting never piles up duplicate copies as prose length changes.</summary>
    public static string StripReadingInfoLine(string description) =>
        ReadingInfoLineRx.Replace(description, "", 1);

    /// <summary>The description with a freshly computed reading line appended (any stale one
    /// removed first).</summary>
    public static string WithReadingLine(string? description, int wordCount)
    {
        var authorPart = StripReadingInfoLine(description ?? "").TrimEnd();
        var readingLine = ReadingLine(wordCount);
        return string.IsNullOrWhiteSpace(authorPart) ? readingLine : $"{authorPart}\n\n{readingLine}";
    }

    /// <summary>Writes <c>description.txt</c> (trimmed) and returns its path.</summary>
    public static async Task<string> WriteDescriptionAsync(string dir, string description, CancellationToken ct = default)
    {
        var path = Path.Combine(dir, DescriptionFileName);
        await File.WriteAllTextAsync(path, description.Trim(), ct);
        return path;
    }

    /// <summary>Writes <c>keywords.txt</c> (one phrase per line) and returns its path, or null when
    /// there are no keywords.</summary>
    public static async Task<string?> WriteKeywordsAsync(string dir, IReadOnlyList<string> keywords, CancellationToken ct = default)
    {
        if (keywords.Count == 0) return null;
        var path = Path.Combine(dir, KeywordsFileName);
        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, keywords), ct);
        return path;
    }
}
