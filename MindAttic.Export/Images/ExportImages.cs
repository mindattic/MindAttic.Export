using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using MindAttic.Export.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MindAttic.Export.Images;

/// <summary>The image encodings the paginated renderers embed.</summary>
public enum ImageKind
{
    Png,
    Jpeg,
    Gif,
    Svg
}

/// <summary>An image's bytes and encoding.</summary>
public sealed record ImageData(byte[] Bytes, ImageKind Kind)
{
    /// <summary>Identifies the encoding from the bytes themselves (magic numbers, or an
    /// <c>&lt;svg</c> root for SVG). Null when the bytes are not an image the renderers support.</summary>
    public static ImageData? FromBytes(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G')
            return new ImageData(bytes, ImageKind.Png);
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return new ImageData(bytes, ImageKind.Jpeg);
        if (bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F')
            return new ImageData(bytes, ImageKind.Gif);
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
        if (head.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return new ImageData(bytes, ImageKind.Svg);
        return null;
    }
}

/// <summary>
/// Turns an <see cref="ImageBlock.Source"/> into bytes at render time. Renderers call it once per
/// figure; a null result prints the alt text in place of the image. Implement it to serve images
/// from an application store, for example <c>media:{uid}</c> sources read from MindAttic.Media's
/// <c>IMediaStore</c>, and chain <see cref="DefaultImageResolver"/> for files and <c>data:</c> URIs.
/// </summary>
public interface IImageResolver
{
    ImageData? Resolve(ImageBlock image);
}

/// <summary>
/// Resolves <c>data:</c> URIs (base64 or percent-encoded), <c>file:</c> URIs, and absolute or
/// relative file paths (relative to <see cref="ImageBlock.BaseDirectory"/>). Any other source
/// (an app scheme such as <c>media:</c>, or <c>http(s):</c>, which an export never downloads) goes
/// to <paramref name="next"/>, or resolves to null when there is none.
/// </summary>
public sealed class DefaultImageResolver(IImageResolver? next = null) : IImageResolver
{
    public static DefaultImageResolver Instance { get; } = new();

    public ImageData? Resolve(ImageBlock image)
    {
        var src = image.Source.Trim();
        if (src.Length == 0) return null;
        try
        {
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return FromDataUri(src);

            string? path = null;
            if (src.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(src, UriKind.Absolute, out var fileUri))
                path = fileUri.LocalPath;
            else if (!HasScheme(src))
            {
                var relative = Uri.UnescapeDataString(src.Split('?', '#')[0]);
                path = Path.IsPathRooted(relative) || image.BaseDirectory is null
                    ? relative
                    : Path.Combine(image.BaseDirectory, relative);
            }
            if (path is not null)
                return File.Exists(path) ? ImageData.FromBytes(File.ReadAllBytes(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            return null;
        }
        return next?.Resolve(image);
    }

    // "C:\x.png" is a rooted path, not a "c:" scheme.
    private static bool HasScheme(string src) =>
        Regex.IsMatch(src, @"^[A-Za-z][A-Za-z0-9+.\-]+:") && !Regex.IsMatch(src, @"^[A-Za-z]:[\\/]");

    private static ImageData? FromDataUri(string uri)
    {
        var comma = uri.IndexOf(',');
        if (comma < 0) return null;
        var meta = uri[5..comma];
        var payload = uri[(comma + 1)..];
        var bytes = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(Regex.Replace(Uri.UnescapeDataString(payload), @"\s", ""))
            : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        return ImageData.FromBytes(bytes);
    }
}

/// <summary>Natural size, fit-to-page sizing and SVG rasterising for the renderers.</summary>
public static class ImageLayout
{
    /// <summary>The image's natural size in points (1/72"), from its own header: PNG pHYs and
    /// JPEG JFIF density (96 dpi when absent), SVG <c>width</c>/<c>height</c> in any CSS unit or
    /// else its <c>viewBox</c> in CSS pixels. Null when the header cannot be read.</summary>
    public static (double Width, double Height)? NaturalSize(ImageData image)
    {
        try
        {
            return image.Kind switch
            {
                ImageKind.Png => Png(image.Bytes),
                ImageKind.Jpeg => Jpeg(image.Bytes),
                ImageKind.Gif => image.Bytes.Length >= 10
                    ? Px(BinaryPrimitives.ReadUInt16LittleEndian(image.Bytes.AsSpan(6)), BinaryPrimitives.ReadUInt16LittleEndian(image.Bytes.AsSpan(8)), 96)
                    : null,
                ImageKind.Svg => Svg(image.Bytes),
                _ => null
            };
        }
        catch (Exception e) when (e is XmlException or ArgumentException or IndexOutOfRangeException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Natural size scaled down (never up) to fit the box, keeping the aspect ratio.
    /// An unreadable size fills the box width at 4:3.</summary>
    public static (double Width, double Height) Fit(ImageData image, double maxWidth, double maxHeight)
    {
        var (w, h) = NaturalSize(image) ?? (maxWidth, maxWidth * 0.75);
        if (w <= 0 || h <= 0) (w, h) = (maxWidth, maxWidth * 0.75);
        var scale = Math.Min(1.0, Math.Min(maxWidth / w, maxHeight / h));
        return (w * scale, h * scale);
    }

    /// <summary>Rasterises an SVG to PNG at the given size and resolution (QuestPDF's renderer,
    /// so no extra dependency). Used for the PNG fallback Word shows where it cannot draw SVG.</summary>
    public static byte[] SvgToPng(byte[] svg, double widthPt, double heightPt, int dpi = 192)
    {
        QuestPDF.Settings.License ??= LicenseType.Community;
        var text = Encoding.UTF8.GetString(svg);
        var images = Document.Create(c => c.Page(p =>
        {
            p.Size(new PageSize((float)widthPt, (float)heightPt));
            p.Margin(0);
            p.PageColor(Colors.White);
            p.Content().Svg(SvgImage.FromText(text));
        })).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = dpi });
        return images.First();
    }

    private static (double, double) Px(double w, double h, double dpi) => (w * 72 / dpi, h * 72 / dpi);

    private static (double, double)? Png(byte[] b)
    {
        if (b.Length < 24) return null;
        var w = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(16));
        var h = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(20));
        double dpiX = 96, dpiY = 96;
        for (var at = 8; at + 12 <= b.Length;)
        {
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(at));
            var type = Encoding.ASCII.GetString(b, at + 4, 4);
            if (type == "pHYs" && len >= 9 && at + 17 <= b.Length && b[at + 16] == 1)
            {
                var ppmX = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(at + 8));
                var ppmY = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(at + 12));
                if (ppmX > 0 && ppmY > 0) { dpiX = ppmX * 0.0254; dpiY = ppmY * 0.0254; }
                break;
            }
            if (type is "IDAT" or "IEND" || len < 0) break;
            at += 12 + len;
        }
        return (w * 72 / dpiX, h * 72 / dpiY);
    }

    private static (double, double)? Jpeg(byte[] b)
    {
        double dpiX = 96, dpiY = 96;
        for (var at = 2; at + 9 < b.Length;)
        {
            if (b[at] != 0xFF) { at++; continue; }
            var marker = b[at + 1];
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7) or 0xFF) { at += marker == 0xFF ? 1 : 2; continue; }
            var len = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(at + 2));
            if (marker == 0xE0 && at + 18 <= b.Length && Encoding.ASCII.GetString(b, at + 4, 4) == "JFIF")
            {
                var units = b[at + 11];
                var dx = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(at + 12));
                var dy = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(at + 14));
                if (dx > 0 && dy > 0 && units is 1 or 2)
                {
                    var f = units == 2 ? 2.54 : 1.0;
                    dpiX = dx * f; dpiY = dy * f;
                }
            }
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var h = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(at + 5));
                var w = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(at + 7));
                return (w * 72 / dpiX, h * 72 / dpiY);
            }
            at += 2 + len;
        }
        return null;
    }

    private static (double, double)? Svg(byte[] b)
    {
        using var reader = XmlReader.Create(new MemoryStream(b), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true
        });
        if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "svg") return null;
        var width = Length(reader.GetAttribute("width"));
        var height = Length(reader.GetAttribute("height"));
        (double W, double H)? box = null;
        var vb = reader.GetAttribute("viewBox")?.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (vb is { Length: 4 }
            && double.TryParse(vb[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var vw)
            && double.TryParse(vb[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var vh)
            && vw > 0 && vh > 0)
            box = (vw, vh);

        if (width is { } w && height is { } h) return (w, h);
        if (box is { } v)
        {
            if (width is { } w2) return (w2, w2 * v.H / v.W);
            if (height is { } h2) return (h2 * v.W / v.H, h2);
            return (v.W * 0.75, v.H * 0.75);   // CSS px → pt
        }
        return null;
    }

    /// <summary>An SVG length in points; null for a percentage or anything unparsable.</summary>
    private static double? Length(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = Regex.Match(value.Trim(), @"^([0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)\s*(px|pt|pc|in|cm|mm|em|)$");
        if (!m.Success) return null;
        var n = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var pt = m.Groups[2].Value switch
        {
            "pt" => n,
            "pc" => n * 12,
            "in" => n * 72,
            "cm" => n * 72 / 2.54,
            "mm" => n * 72 / 25.4,
            "em" => n * 12,
            _ => n * 0.75   // px or unitless: CSS pixels at 96 per inch
        };
        return pt > 0 ? pt : null;
    }
}
