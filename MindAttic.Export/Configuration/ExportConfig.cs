using System.Text.Json;
using System.Text.Json.Serialization;
using MindAttic.Export.Markdown;

namespace MindAttic.Export.Configuration;

/// <summary>
/// A Markdown-document export described in JSON (e.g. <c>export.json</c> in a manuscript repo).
/// Relative <see cref="Sources"/> resolve against the config file's folder.
/// </summary>
public sealed record ExportConfig
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public List<string> Keywords { get; init; } = [];

    /// <summary>Markdown files or wildcard patterns, in reading order. A pattern's matches are
    /// sorted ordinally (so <c>chapters/*.md</c> follows the numeric file prefixes).</summary>
    public List<string> Sources { get; init; } = [];

    public required string OutputDirectory { get; init; }
    public required string FileBaseName { get; init; }
    public List<string> Formats { get; init; } = ["docx", "pdf", "txt", "md"];
    public int? Version { get; init; }
    public bool Archive { get; init; } = true;
    public bool Sidecars { get; init; } = true;

    /// <summary><c>letter</c> (default for documents) or <c>trade</c> (6"×9" Prose book trim).</summary>
    public string Profile { get; init; } = "letter";
    public bool Toc { get; init; } = true;
    public bool DocxPageNumbers { get; init; } = true;
    public string FontFamily { get; init; } = "Garamond";

    /// <summary><c>em-dash</c> (house style) or <c>as-written</c>.</summary>
    public string HeadingStyle { get; init; } = "em-dash";
    public List<string>? HangingIndentChapters { get; init; }
    public bool Math { get; init; } = true;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ExportConfig Load(string path) =>
        JsonSerializer.Deserialize<ExportConfig>(File.ReadAllText(path), Json)
        ?? throw new InvalidDataException($"Empty export config: {path}");

    public ExportOptions ToOptions() => new()
    {
        Profile = Profile.Trim().ToLowerInvariant() is "trade" or "trade6x9" or "6x9" ? PageProfile.Trade6x9 : PageProfile.Letter,
        IncludeToc = Toc,
        DocxPageNumbers = DocxPageNumbers,
        FontFamily = FontFamily
    };

    public MarkdownReaderOptions ToReaderOptions() => new()
    {
        HeadingStyle = HeadingStyle.Trim().ToLowerInvariant() is "as-written" or "aswritten" ? ChapterHeadingStyle.AsWritten : ChapterHeadingStyle.EmDash,
        HangingIndentChapters = HangingIndentChapters ?? new MarkdownReaderOptions().HangingIndentChapters,
        Math = Math
    };

    public ManuscriptInfo ToInfo() => new()
    {
        Title = Title,
        Subtitle = Subtitle,
        Author = Author,
        Description = Description,
        Keywords = Keywords
    };

    /// <summary>The output folder; a relative path resolves against the config file's folder,
    /// like <see cref="Sources"/>.</summary>
    public string ResolveOutputDirectory(string baseDirectory) =>
        Path.GetFullPath(Path.IsPathRooted(OutputDirectory) ? OutputDirectory : Path.Combine(baseDirectory, OutputDirectory));

    /// <summary>The source files in reading order.</summary>
    public IReadOnlyList<string> ResolveSources(string baseDirectory)
    {
        var files = new List<string>();
        foreach (var source in Sources)
        {
            var full = Path.IsPathRooted(source) ? source : Path.Combine(baseDirectory, source);
            var name = Path.GetFileName(full);
            if (name.Contains('*') || name.Contains('?'))
            {
                var dir = Path.GetDirectoryName(full)!;
                if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
                files.AddRange(Directory.GetFiles(dir, name).OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal));
            }
            else
            {
                if (!File.Exists(full)) throw new FileNotFoundException("Export source not found.", full);
                files.Add(full);
            }
        }
        return files.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
