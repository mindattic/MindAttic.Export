using System.Text.RegularExpressions;

namespace MindAttic.Export.Paths;

/// <summary>
/// File-system naming for export bundles. Migrated from <c>Prose.Core.Services.ExportPathResolver</c>
/// (the database-free half; resolving a node's code or ancestry stays in Prose).
///
/// <para>Bundle convention: <c>&lt;root&gt;\&lt;CODE&gt;\&lt;CODE&gt; V&lt;N&gt;.&lt;ext&gt;</c>, e.g.
/// <c>NONFICTION\1381\1381 V13.docx</c>. The full title appears only inside the documents.</para>
/// </summary>
public static class ExportPaths
{
    /// <summary><c>"{fileBaseName} V{version}.{extension}"</c>.</summary>
    public static string BundleFileName(string fileBaseName, int version, string extension) =>
        $"{fileBaseName} V{version}.{extension.TrimStart('.')}";

    public static string SanitizeTitle(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        // The Windows set explicitly: GetInvalidFileNameChars() is only '\0' and '/' off Windows,
        // and the exported folder is synced/opened on Windows either way.
        foreach (var c in "\\/:*?\"<>|") invalid.Add(c);
        invalid.Add('\''); invalid.Add('’');
        var kept = new string((title ?? "").Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim();
        kept = Regex.Replace(kept, @"\s+", " ").Trim();
        // Trailing dots/spaces are silently dropped by Windows (so "And Then..." and "And Then"
        // collided), and a title of "." or ".." would climb out of the export folder.
        kept = kept.TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(kept)) return "untitled";
        return IsReservedDeviceName(kept) ? "_" + kept : kept;
    }

    /// <summary>True for a Windows reserved device name (CON, PRN, AUX, NUL, COM0-9, LPT0-9), with
    /// or without an extension — "Con" or "nul.txt" as a folder/file name opens the device
    /// instead of creating the file.</summary>
    public static bool IsReservedDeviceName(string name)
    {
        var stem = name.Split('.')[0].TrimEnd(' ');
        return stem.ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                                     || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && (char.IsAsciiDigit(stem[3]) || stem[3] is '¹' or '²' or '³'));
    }
}
