using System.Globalization;
using System.Text;
using System.Text.Json;
using MindAttic.Export.Paths;

namespace MindAttic.Export.Artifacts;

/// <summary>What happens when an artifact's target file already exists.</summary>
public enum ExistingArtifact
{
    /// <summary>Move the existing file into <c>Archives\</c> beside it (timestamp-suffixed), then
    /// write. Nothing is ever deleted. The default.</summary>
    Archive,

    /// <summary>Write under a de-duplicated name (<c>name (2).ext</c>, <c>name (3).ext</c>, …).</summary>
    Uniquify,

    /// <summary>Replace the file in place (atomically).</summary>
    Overwrite,

    /// <summary>Throw <see cref="IOException"/>.</summary>
    Fail
}

public sealed record ArtifactOptions
{
    public ExistingArtifact Existing { get; init; } = ExistingArtifact.Archive;

    /// <summary>Write to a temporary file in the target folder and move it into place, so a
    /// reader never sees a half-written artifact. On by default.</summary>
    public bool Atomic { get; init; } = true;

    /// <summary>Sanitize the file name (Windows-invalid characters, reserved device names).</summary>
    public bool SanitizeName { get; init; } = true;

    /// <summary>Text encoding; UTF-8 without BOM by default.</summary>
    public Encoding Encoding { get; init; } = new UTF8Encoding(false);

    public static ArtifactOptions Default { get; } = new();
}

/// <summary>
/// The one place a MindAttic application turns in-memory data into a user-facing file (a report,
/// recording, data export, document). Every artifact gets the same guarantees: a sanitized
/// name, the folder created, an atomic write, UTF-8 without BOM for text, and the "archive,
/// never delete" rule for a file that already exists.
/// </summary>
public static class ArtifactWriter
{
    public static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static Task<string> WriteTextAsync(string directory, string fileName, string content,
                                              ArtifactOptions? options = null, CancellationToken ct = default)
    {
        options ??= ArtifactOptions.Default;
        var bytes = options.Encoding.GetPreamble().Concat(options.Encoding.GetBytes(content)).ToArray();
        return WriteBytesAsync(directory, fileName, bytes, options, ct);
    }

    public static Task<string> WriteJsonAsync<T>(string directory, string fileName, T value,
                                                 JsonSerializerOptions? json = null,
                                                 ArtifactOptions? options = null, CancellationToken ct = default) =>
        WriteTextAsync(directory, fileName, JsonSerializer.Serialize(value, json ?? IndentedJson), options, ct);

    public static Task<string> WriteBytesAsync(string directory, string fileName, byte[] bytes,
                                               ArtifactOptions? options = null, CancellationToken ct = default) =>
        WriteStreamAsync(directory, fileName, (s, c) => s.WriteAsync(bytes, c).AsTask(), options, ct);

    /// <summary>Writes an artifact whose content is produced by <paramref name="write"/>. Returns the
    /// final path.</summary>
    public static async Task<string> WriteStreamAsync(string directory, string fileName,
                                                      Func<Stream, CancellationToken, Task> write,
                                                      ArtifactOptions? options = null, CancellationToken ct = default)
    {
        options ??= ArtifactOptions.Default;
        var target = await PrepareTargetAsync(directory, fileName, options);
        if (!options.Atomic)
        {
            await using var direct = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            await write(direct, ct);
            return target;
        }

        var temp = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await write(stream, ct);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return target;
    }

    /// <summary>Writes an artifact produced by a renderer that insists on a file path (OpenXml,
    /// QuestPDF). The renderer writes a temporary path that is then moved into place.</summary>
    public static async Task<string> WriteViaPathAsync(string directory, string fileName,
                                                       Func<string, CancellationToken, Task> render,
                                                       ArtifactOptions? options = null, CancellationToken ct = default)
    {
        options ??= ArtifactOptions.Default;
        var target = await PrepareTargetAsync(directory, fileName, options);
        if (!options.Atomic)
        {
            await render(target, ct);
            return target;
        }
        var temp = Path.Combine(Path.GetDirectoryName(target)!, $".{Guid.NewGuid():N}{Path.GetExtension(target)}.tmp");
        try
        {
            await render(temp, ct);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return target;
    }

    private static Task<string> PrepareTargetAsync(string directory, string fileName, ArtifactOptions options)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("An artifact needs a directory.", nameof(directory));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("An artifact needs a file name.", nameof(fileName));
        var name = options.SanitizeName ? SanitizeFileName(fileName) : fileName;
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, name);

        if (File.Exists(target))
        {
            switch (options.Existing)
            {
                case ExistingArtifact.Fail:
                    throw new IOException($"Artifact already exists: {target}");
                case ExistingArtifact.Uniquify:
                    target = UniquePath(directory, name);
                    break;
                case ExistingArtifact.Archive:
                    ArchiveExisting(target);
                    break;
                case ExistingArtifact.Overwrite:
                    break;
            }
        }
        return Task.FromResult(target);
    }

    /// <summary>Moves an existing file into <c>Archives\</c> beside it with a UTC timestamp suffix.</summary>
    public static string ArchiveExisting(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var archive = Path.Combine(dir, ExportArchive.ArchivesFolder);
        Directory.CreateDirectory(archive);
        var stamped = Path.Combine(archive,
            $"{Path.GetFileNameWithoutExtension(path)}__{DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}{Path.GetExtension(path)}");
        File.Move(path, stamped);
        return stamped;
    }

    private static string UniquePath(string directory, string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var n = 2; ; n++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({n}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    /// <summary>A file name made safe for Windows. Unlike <see cref="ExportPaths.SanitizeTitle"/>,
    /// dots inside the name (multi-part extensions) are kept.</summary>
    public static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        foreach (var c in "\\/:*?\"<>|") invalid.Add(c);
        var kept = new string(fileName.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimEnd('.', ' ');
        if (kept.Length == 0) return "untitled";
        return ExportPaths.IsReservedDeviceName(kept) ? "_" + kept : kept;
    }
}

/// <summary>Conventional artifact names.</summary>
public static class ArtifactNames
{
    /// <summary><c>{prefix}-{yyyyMMdd-HHmmss}.{kind}.{extension}</c>, e.g.
    /// <c>KdpPublish-session-20261004-192103.autowebnav-recording.json</c>. The kind segment is
    /// omitted when null.</summary>
    public static string Timestamped(string prefix, DateTime timestamp, string extension, string? kind = null)
    {
        var stamp = timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var ext = extension.TrimStart('.');
        return kind is null ? $"{prefix}-{stamp}.{ext}" : $"{prefix}-{stamp}.{kind}.{ext}";
    }
}
