namespace MindAttic.Export;

/// <summary>Physical page geometry for the paginated formats (docx, pdf).</summary>
public enum PageProfile
{
    /// <summary>Prose house trim: 6" × 9" KDP paperback. Docx uses mirror margins with a
    /// page-count-dependent gutter; PDF uses 1" top/bottom and 0.75" sides.</summary>
    Trade6x9,

    /// <summary>US Letter, 1" margins on every side. For reports and academic documents.</summary>
    Letter
}

/// <summary>Rendering switches. The defaults reproduce Prose's book exports exactly.</summary>
public sealed record ExportOptions
{
    public PageProfile Profile { get; init; } = PageProfile.Trade6x9;

    /// <summary>Emit a table of contents in the docx (only when there are two or more chapters,
    /// Prose's <c>DocxIncludeToc</c> rule) and a page-numbered contents page in the PDF.</summary>
    public bool IncludeToc { get; init; }

    /// <summary>
    /// The chapter count the docx renderer uses to decide whether headings are printed at all
    /// (a single-chapter story prints none), whether the TOC is emitted, and how much page
    /// overhead to add to the KDP page estimate. Prose passes its spine's count; when null,
    /// the number of chapters with a heading is used.
    /// </summary>
    public int? ChapterCount { get; init; }

    /// <summary>Centered page numbers in the docx footer. Prose's KDP manuscript has none.</summary>
    public bool DocxPageNumbers { get; init; }

    /// <summary>Base font family for docx and pdf.</summary>
    public string FontFamily { get; init; } = "Garamond";

    /// <summary>Fixed EPUB <c>dc:identifier</c>. Null (Prose behaviour) mints a new
    /// <c>urn:uuid</c> on every export.</summary>
    public string? BookIdentifier { get; init; }

    /// <summary>Fixed timestamp for EPUB <c>dcterms:modified</c> and PDF metadata, for
    /// reproducible output in tests and verification. Null uses the current UTC time.</summary>
    public DateTime? FixedTimestamp { get; init; }

    public static ExportOptions ProseBook { get; } = new();

    public static ExportOptions Document { get; } = new()
    {
        Profile = PageProfile.Letter,
        IncludeToc = true,
        DocxPageNumbers = true
    };
}

/// <summary>An export format the library can render.</summary>
public enum ExportFormat
{
    Docx,
    Epub,
    Pdf,
    Txt,
    Md
}

public static class ExportFormatExtensions
{
    public static string Extension(this ExportFormat format) => format switch
    {
        ExportFormat.Docx => "docx",
        ExportFormat.Epub => "epub",
        ExportFormat.Pdf => "pdf",
        ExportFormat.Txt => "txt",
        ExportFormat.Md => "md",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static ExportFormat Parse(string value) => value.Trim().TrimStart('.').ToLowerInvariant() switch
    {
        "docx" => ExportFormat.Docx,
        "epub" => ExportFormat.Epub,
        "pdf" => ExportFormat.Pdf,
        "txt" => ExportFormat.Txt,
        "md" or "markdown" => ExportFormat.Md,
        _ => throw new ArgumentException($"Unknown export format '{value}'. Expected docx, epub, pdf, txt or md.")
    };
}
