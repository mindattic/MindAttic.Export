using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MindAttic.Export.Tests.Support;

/// <summary>One zip entry as stored on disk (central directory) plus its decompressed bytes.</summary>
public sealed record ZipEntryInfo(string Name, ushort Method, long CompressedLength, long Length, byte[] Content)
{
    public string Text => new UTF8Encoding(false).GetString(Content);
}

public static class Packages
{
    /// <summary>Entries in central-directory order with their raw compression method
    /// (0 = stored, 8 = deflate).</summary>
    public static List<ZipEntryInfo> ReadZip(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var methods = CentralDirectory(bytes);
        var list = new List<ZipEntryInfo>();
        using var zip = ZipFile.OpenRead(path);
        var i = 0;
        foreach (var entry in zip.Entries)
        {
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var (name, method) = methods[i++];
            if (name != entry.FullName) throw new InvalidDataException($"Central directory order mismatch: {name} vs {entry.FullName}");
            list.Add(new ZipEntryInfo(entry.FullName, method, entry.CompressedLength, entry.Length, ms.ToArray()));
        }
        return list;
    }

    /// <summary>(name, method) for every central-directory record, in order.</summary>
    public static List<(string Name, ushort Method)> CentralDirectory(byte[] zip)
    {
        var eocd = -1;
        for (var p = zip.Length - 22; p >= 0; p--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(p)) == 0x06054b50) { eocd = p; break; }
        if (eocd < 0) throw new InvalidDataException("No end-of-central-directory record.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(eocd + 10));
        var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(eocd + 16));
        var result = new List<(string, ushort)>();
        for (var k = 0; k < count; k++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset)) != 0x02014b50) throw new InvalidDataException("Bad central directory record.");
            var method = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 10));
            var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 28));
            var extraLen = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 30));
            var commentLen = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 32));
            result.Add((Encoding.UTF8.GetString(zip, offset + 46, nameLen), method));
            offset += 46 + nameLen + extraLen + commentLen;
        }
        return result;
    }

    /// <summary>The first local file header: (name, method). EPUB requires "mimetype", stored.</summary>
    public static (string Name, ushort Method, int ExtraLength) FirstLocalEntry(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x04034b50) throw new InvalidDataException("Not a zip.");
        var method = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8));
        var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26));
        var extraLen = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
        return (Encoding.ASCII.GetString(bytes, 30, nameLen), method, extraLen);
    }
}

public static class Pdf
{
    private static readonly Regex PageObject = new(@"/Type\s*/Page(?![a-zA-Z])", RegexOptions.Compiled);
    private static readonly Regex Dates = new(@"/(CreationDate|ModDate)\s*\((?:\\.|[^)\\])*\)", RegexOptions.Compiled);
    private static readonly Regex DocId = new(@"/ID\s*\[\s*<[0-9A-Fa-f]*>\s*<[0-9A-Fa-f]*>\s*\]", RegexOptions.Compiled);

    public static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    public static bool LooksValid(byte[] bytes)
    {
        if (bytes.Length < 16) return false;
        var text = Latin1(bytes);
        return text.StartsWith("%PDF-", StringComparison.Ordinal) && text.TrimEnd('\r', '\n', ' ', '\0').EndsWith("%%EOF", StringComparison.Ordinal);
    }

    public static int PageCount(byte[] bytes) => PageObject.Matches(Latin1(bytes)).Count;

    /// <summary>The file with creation/modification dates and the trailer document ID blanked,
    /// keeping every byte offset unchanged (each value is replaced by the same number of 'X').</summary>
    public static string Normalised(byte[] bytes)
    {
        var text = Latin1(bytes);
        text = Dates.Replace(text, m => $"/{m.Groups[1].Value}" + new string('X', m.Length - m.Groups[1].Length - 1));
        text = DocId.Replace(text, m => new string('X', m.Length));
        return text;
    }
}
