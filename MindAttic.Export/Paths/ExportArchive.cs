using System.Globalization;
using System.Text.RegularExpressions;

namespace MindAttic.Export.Paths;

/// <summary>
/// "Archive, never delete" for export bundles. Migrated from
/// <c>Prose.Core.Services.ExportCleanupService</c> (the file-system half).
///
/// <para>Before a new version is written, <see cref="Clean"/> copies every top-level file from
/// <c>nodeDir</c> into <c>archiveDir\v&lt;N&gt;\</c> (N read from the file name, else the
/// fallback) and removes the live copy — except <c>cover.jpg</c>, which is author-supplied and
/// stays. After the new bundle is written, <see cref="ArchiveCurrent"/> copies it into its own
/// version folder so the newest export is protected even if no later export happens. A name
/// already present in the archive gets a <c>__yyyyMMddHHmmssfff</c> suffix instead of
/// overwriting.</para>
///
/// <para><c>archiveDir</c> is caller-resolved on purpose: this library has no opinion on WHERE a
/// caller's archive tree lives (nested under the bundle folder, or a sibling of it entirely) —
/// only on how a version is archived once that location is known. Prose keeps its book archives
/// in a tree that mirrors each universe's export root, one level up
/// (<c>ExportCleanupService</c>); <see cref="ManuscriptExporter.ExportBundleAsync"/> keeps the
/// older nested <c>nodeDir\Archives\</c> shape for its own generic callers.</para>
/// </summary>
public static class ExportArchive
{
    public const string ArchivesFolder = "Archives";
    public const string CoverFileName = "cover.jpg";

    private static readonly Regex VersionInName = new(@"(?:^|\s)[Vv](\d+)(?:\.|\s|$)", RegexOptions.Compiled);
    private static readonly Regex VersionFolder = new(@"^v(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Copies existing export artifacts from <paramref name="nodeDir"/> into
    /// <paramref name="archiveDir"/>\v&lt;version&gt;, then removes the live copies.</summary>
    public static void Clean(string nodeDir, string archiveDir, int? fallbackVersion = null)
    {
        Directory.CreateDirectory(nodeDir);
        foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var version = ExtractVersion(name) ?? fallbackVersion ?? 0;
            var versionDir = Path.Combine(archiveDir, $"v{version}");
            Directory.CreateDirectory(versionDir);
            var destination = Path.Combine(versionDir, name);
            if (File.Exists(destination)) destination = Path.Combine(versionDir, $"{Path.GetFileNameWithoutExtension(name)}__{Stamp()}{Path.GetExtension(name)}");
            try
            {
                File.Copy(file, destination, overwrite: false);
                if (!name.Equals(CoverFileName, StringComparison.OrdinalIgnoreCase)) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Copies the completed live export bundle in <paramref name="nodeDir"/> into its
    /// permanent version archive under <paramref name="archiveDir"/>.</summary>
    public static void ArchiveCurrent(string nodeDir, string archiveDir, int version)
    {
        Directory.CreateDirectory(nodeDir);
        var versionDir = Path.Combine(archiveDir, $"v{version}");
        Directory.CreateDirectory(versionDir);
        foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var destination = Path.Combine(versionDir, name);
            if (File.Exists(destination))
                destination = Path.Combine(versionDir, $"{Path.GetFileNameWithoutExtension(name)}__{Stamp()}{Path.GetExtension(name)}");
            try { File.Copy(file, destination, overwrite: false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // Invariant: under a culture whose default calendar is not Gregorian (th-TH, ar-SA) the
    // current-culture "yyyy" wrote a different year into the archive name than ArtifactWriter does.
    private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);

    /// <summary>The <c>V&lt;N&gt;</c> in a bundle file name, if any.</summary>
    public static int? ExtractVersion(string fileName)
    {
        var match = VersionInName.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, out var v) ? v : null;
    }

    /// <summary>
    /// The highest version present in a bundle folder — in <paramref name="nodeDir"/>'s live file
    /// names or as a <c>v&lt;N&gt;</c> folder under <paramref name="archiveDir"/> — or 0 when
    /// there is none. For consumers that, unlike Prose, keep no version counter of their own: the
    /// next export is this plus one.
    /// </summary>
    public static int CurrentVersion(string nodeDir, string archiveDir)
    {
        var max = 0;
        if (Directory.Exists(nodeDir))
            foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
                if (ExtractVersion(Path.GetFileName(file)) is int v && v > max) max = v;
        if (Directory.Exists(archiveDir))
            foreach (var dir in Directory.EnumerateDirectories(archiveDir))
            {
                var m = VersionFolder.Match(Path.GetFileName(dir));
                if (m.Success && int.TryParse(m.Groups[1].Value, out var v) && v > max) max = v;
            }
        return max;
    }
}
