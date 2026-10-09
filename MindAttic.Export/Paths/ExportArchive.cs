using System.Text.RegularExpressions;

namespace MindAttic.Export.Paths;

/// <summary>
/// "Archive, never delete" for export bundles. Migrated from
/// <c>Prose.Core.Services.ExportCleanupService</c> (the file-system half).
///
/// <para>Before a new version is written, <see cref="Clean"/> copies every top-level file into
/// <c>Archives\v&lt;N&gt;\</c> (N read from the file name, else the fallback) and removes the live
/// copy — except <c>cover.jpg</c>, which is author-supplied and stays. After the new bundle is
/// written, <see cref="ArchiveCurrent"/> copies it into its own version folder so the newest
/// export is protected even if no later export happens. A name already present in the archive
/// gets a <c>__yyyyMMddHHmmssfff</c> suffix instead of overwriting.</para>
/// </summary>
public static class ExportArchive
{
    public const string ArchivesFolder = "Archives";
    public const string CoverFileName = "cover.jpg";

    private static readonly Regex VersionInName = new(@"(?:^|\s)[Vv](\d+)(?:\.|\s|$)", RegexOptions.Compiled);
    private static readonly Regex VersionFolder = new(@"^v(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Copies existing export artifacts into Archives\v&lt;version&gt;, then removes live copies.</summary>
    public static void Clean(string nodeDir, int? fallbackVersion = null)
    {
        Directory.CreateDirectory(nodeDir);
        foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var version = ExtractVersion(name) ?? fallbackVersion ?? 0;
            var archiveDir = Path.Combine(nodeDir, ArchivesFolder, $"v{version}");
            Directory.CreateDirectory(archiveDir);
            var destination = Path.Combine(archiveDir, name);
            if (File.Exists(destination)) destination = Path.Combine(archiveDir, $"{Path.GetFileNameWithoutExtension(name)}__{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(name)}");
            try
            {
                File.Copy(file, destination, overwrite: false);
                if (!name.Equals(CoverFileName, StringComparison.OrdinalIgnoreCase)) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Copies the completed live export bundle into its permanent version archive.</summary>
    public static void ArchiveCurrent(string nodeDir, int version)
    {
        Directory.CreateDirectory(nodeDir);
        var archiveDir = Path.Combine(nodeDir, ArchivesFolder, $"v{version}");
        Directory.CreateDirectory(archiveDir);
        foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            var destination = Path.Combine(archiveDir, name);
            if (File.Exists(destination))
                destination = Path.Combine(archiveDir, $"{Path.GetFileNameWithoutExtension(name)}__{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(name)}");
            try { File.Copy(file, destination, overwrite: false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The <c>V&lt;N&gt;</c> in a bundle file name, if any.</summary>
    public static int? ExtractVersion(string fileName)
    {
        var match = VersionInName.Match(fileName);
        return match.Success && int.TryParse(match.Groups[1].Value, out var v) ? v : null;
    }

    /// <summary>
    /// The highest version present in a bundle folder — in live file names or as an
    /// <c>Archives\v&lt;N&gt;</c> folder — or 0 when there is none. For consumers that, unlike
    /// Prose, keep no version counter of their own: the next export is this plus one.
    /// </summary>
    public static int CurrentVersion(string nodeDir)
    {
        if (!Directory.Exists(nodeDir)) return 0;
        var max = 0;
        foreach (var file in Directory.EnumerateFiles(nodeDir, "*", SearchOption.TopDirectoryOnly))
            if (ExtractVersion(Path.GetFileName(file)) is int v && v > max) max = v;
        var archives = Path.Combine(nodeDir, ArchivesFolder);
        if (Directory.Exists(archives))
            foreach (var dir in Directory.EnumerateDirectories(archives))
            {
                var m = VersionFolder.Match(Path.GetFileName(dir));
                if (m.Success && int.TryParse(m.Groups[1].Value, out var v) && v > max) max = v;
            }
        return max;
    }
}
