using System.Text;
using DocumentFormat.OpenXml.Packaging;
using MindAttic.Export.Images;
using MindAttic.Export.Markdown;
using MindAttic.Export.Model;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Structural;
using MindAttic.Export.Tests.Support;
using A = DocumentFormat.OpenXml.Drawing;

namespace MindAttic.Export.Tests.Images;

[TestFixture]
public class ImageExportTests : TempDirTestBase
{
    // 1 × 1 PNG, no pHYs chunk (so 96 dpi: 0.75pt square).
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
    private static readonly byte[] Png = Convert.FromBase64String(PngBase64);

    private const string Svg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="4in" height="2in" viewBox="0 0 400 200"><rect x="10" y="10" width="380" height="180" fill="#3a6ea5"/><text x="200" y="110" font-size="24" text-anchor="middle" fill="#fff">Figure</text></svg>""";

    private static Manuscript One(params Block[] blocks) => new() { Title = "T", Chapters = [new Chapter("C", [.. blocks])] };

    // ── Reading ──────────────────────────────────────────────────────────────

    [Test]
    public void A_paragraph_that_is_only_an_image_is_a_figure()
    {
        var chapters = MarkdownManuscriptReader.ReadChapters(
            "# One\n\n![The *bargaining* set](figures/set.svg \"Set\")\n\nText ![inline](x.png) here.\n",
            new MarkdownReaderOptions { BaseDirectory = @"D:\book" });
        var blocks = chapters.Single().Blocks;
        Assert.That(blocks[0], Is.EqualTo(new ImageBlock("figures/set.svg", "The bargaining set", "Set", @"D:\book")));
        Assert.That(blocks[1], Is.TypeOf<ParagraphBlock>());
    }

    [Test]
    public void ReadFiles_resolves_relative_images_against_each_file()
    {
        var dir = Temp.Sub("chapters");
        Directory.CreateDirectory(dir);
        var md = Path.Combine(dir, "01.md");
        File.WriteAllText(md, "# One\n\n![a](fig.png)\n");
        var m = MarkdownManuscriptReader.ReadFiles([md], new ManuscriptInfo { Title = "T" });
        Assert.That(((ImageBlock)m.Chapters[0].Blocks[0]).BaseDirectory, Is.EqualTo(dir));
    }

    [Test]
    public void Markdown_writes_the_figure_back()
    {
        var md = MarkdownRenderer.Render(One(new ImageBlock("figures/a b.svg", "Alt", "Say \"hi\"")));
        Assert.That(md, Does.Contain("![Alt](<figures/a b.svg> \"Say \\\"hi\\\"\")"));
    }

    // ── Resolving ────────────────────────────────────────────────────────────

    [Test]
    public void Resolves_base64_data_uris()
    {
        var data = DefaultImageResolver.Instance.Resolve(new ImageBlock($"data:image/png;base64,{PngBase64}", "a"));
        Assert.That(data?.Kind, Is.EqualTo(ImageKind.Png));
        Assert.That(data!.Bytes, Is.EqualTo(Png));
    }

    [Test]
    public void Resolves_percent_encoded_svg_data_uris()
    {
        var data = DefaultImageResolver.Instance.Resolve(new ImageBlock("data:image/svg+xml," + Uri.EscapeDataString(Svg), "a"));
        Assert.That(data?.Kind, Is.EqualTo(ImageKind.Svg));
    }

    [Test]
    public void Resolves_relative_and_absolute_paths()
    {
        File.WriteAllText(Temp.File("f.svg"), Svg);
        Assert.That(DefaultImageResolver.Instance.Resolve(new ImageBlock("f.svg", "", BaseDirectory: Temp.Path))?.Kind, Is.EqualTo(ImageKind.Svg));
        Assert.That(DefaultImageResolver.Instance.Resolve(new ImageBlock(Temp.File("f.svg"), ""))?.Kind, Is.EqualTo(ImageKind.Svg));
        Assert.That(DefaultImageResolver.Instance.Resolve(new ImageBlock("missing.svg", "", BaseDirectory: Temp.Path)), Is.Null);
    }

    [Test]
    public void Unknown_schemes_go_to_the_next_resolver_and_are_never_downloaded()
    {
        var store = new StoreResolver();
        var resolver = new DefaultImageResolver(store);
        Assert.That(resolver.Resolve(new ImageBlock("media:0f8e", ""))?.Kind, Is.EqualTo(ImageKind.Png));
        Assert.That(DefaultImageResolver.Instance.Resolve(new ImageBlock("https://example.com/a.png", "")), Is.Null);
        Assert.That(store.Asked, Is.EqualTo(["media:0f8e"]));
    }

    private sealed class StoreResolver : IImageResolver
    {
        public List<string> Asked { get; } = [];
        public ImageData? Resolve(ImageBlock image)
        {
            Asked.Add(image.Source);
            return image.Source.StartsWith("media:") ? new ImageData(Png, ImageKind.Png) : null;
        }
    }

    // ── Sizing ───────────────────────────────────────────────────────────────

    [TestCase("""width="6in" height="3in" """, 432, 216)]
    [TestCase("""width="96px" height="48" """, 72, 36)]
    [TestCase("""viewBox="0 0 400 200" """, 300, 150)]
    [TestCase("""width="2in" viewBox="0 0 400 200" """, 144, 72)]
    [TestCase("""width="100%" height="100%" viewBox="0,0,80,40" """, 60, 30)]
    public void Svg_natural_size_in_points(string attributes, double w, double h)
    {
        var size = ImageLayout.NaturalSize(new ImageData(Encoding.UTF8.GetBytes($"<svg xmlns=\"http://www.w3.org/2000/svg\" {attributes}/>"), ImageKind.Svg));
        Assert.That(size, Is.EqualTo((w, h)));
    }

    [Test]
    public void Png_natural_size_defaults_to_96_dpi() =>
        Assert.That(ImageLayout.NaturalSize(new ImageData(Png, ImageKind.Png)), Is.EqualTo((0.75, 0.75)));

    [Test]
    public void Fit_scales_down_keeping_aspect_and_never_up()
    {
        var wide = new ImageData(Encoding.UTF8.GetBytes("""<svg width="10in" height="5in"/>"""), ImageKind.Svg);
        Assert.That(ImageLayout.Fit(wide, 468, 500), Is.EqualTo((468.0, 234.0)));
        Assert.That(ImageLayout.Fit(new ImageData(Png, ImageKind.Png), 468, 500), Is.EqualTo((0.75, 0.75)));
    }

    // ── DOCX ─────────────────────────────────────────────────────────────────

    [Test]
    public void Docx_embeds_svg_with_a_png_fallback()
    {
        File.WriteAllText(Temp.File("f.svg"), Svg);
        var path = Temp.File("out.docx");
        DocxRenderer.Render(One(new ImageBlock("f.svg", "A figure", BaseDirectory: Temp.Path)), path, ExportOptions.Document);

        Assert.That(StructuralValidityTests.Validate(path), Is.Empty);
        using var doc = WordprocessingDocument.Open(path, false);
        var main = doc.MainDocumentPart!;
        var types = main.ImageParts.Select(p => p.ContentType).OrderBy(t => t).ToList();
        Assert.That(types, Is.EqualTo(new[] { "image/png", "image/svg+xml" }));

        var blip = main.Document!.Descendants<A.Blip>().Single();
        Assert.That(main.GetPartById(blip.Embed!.Value!).ContentType, Is.EqualTo("image/png"));
        var svgBlip = blip.Descendants<DocumentFormat.OpenXml.Office2019.Drawing.SVG.SVGBlip>().Single();
        Assert.That(main.GetPartById(svgBlip.Embed!.Value!).ContentType, Is.EqualTo("image/svg+xml"));

        var extent = main.Document.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>().Single();
        Assert.That((extent.Cx!.Value, extent.Cy!.Value), Is.EqualTo((288L * 12700, 144L * 12700)));
        var docPr = main.Document.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Single();
        Assert.That(docPr.Description?.Value, Is.EqualTo("A figure"));

        using var png = main.GetPartById(blip.Embed!.Value!).GetStream();
        var header = new byte[4];
        png.ReadExactly(header);
        Assert.That(header, Is.EqualTo(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }));
    }

    [Test]
    public void Docx_embeds_raster_images_and_names_each_figure_uniquely()
    {
        var path = Temp.File("out.docx");
        var src = $"data:image/png;base64,{PngBase64}";
        DocxRenderer.Render(One(new ImageBlock(src, "one"), new ImageBlock(src, "two")), path, ExportOptions.Document);

        Assert.That(StructuralValidityTests.Validate(path), Is.Empty);
        using var doc = WordprocessingDocument.Open(path, false);
        Assert.That(doc.MainDocumentPart!.ImageParts.Count(), Is.EqualTo(2));
        var ids = doc.MainDocumentPart.Document!.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties>().Select(d => d.Id!.Value).ToList();
        Assert.That(ids, Is.Unique);
    }

    [Test]
    public void An_unresolvable_figure_prints_its_alt_text_instead_of_vanishing()
    {
        var path = Temp.File("out.docx");
        DocxRenderer.Render(One(new ImageBlock("nowhere.svg", "Lost figure", BaseDirectory: Temp.Path)), path, ExportOptions.Document);
        using var doc = WordprocessingDocument.Open(path, false);
        Assert.That(doc.MainDocumentPart!.Document!.Body!.InnerText, Does.Contain("[Figure: Lost figure]"));
        Assert.That(TextRenderer.Render(One(new ImageBlock("x.svg", "Lost figure"))), Does.Contain("[Figure: Lost figure]"));
    }

    [Test]
    public void Docx_uses_the_supplied_resolver()
    {
        var path = Temp.File("out.docx");
        var store = new StoreResolver();
        DocxRenderer.Render(One(new ImageBlock("media:abc", "m")), path, ExportOptions.Document with { ImageResolver = new DefaultImageResolver(store) });
        using var doc = WordprocessingDocument.Open(path, false);
        Assert.That(doc.MainDocumentPart!.ImageParts.Count(), Is.EqualTo(1));
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    [Test]
    public void Pdf_draws_svg_as_vector_and_embeds_raster_images()
    {
        File.WriteAllText(Temp.File("f.svg"), Svg);
        var withFigures = Temp.File("figures.pdf");
        var without = Temp.File("plain.pdf");
        var options = ExportOptions.Document with { FixedTimestamp = new DateTime(2026, 1, 1) };
        PdfRenderer.Render(One(new ImageBlock("f.svg", "svg", BaseDirectory: Temp.Path),
                               new ImageBlock($"data:image/png;base64,{PngBase64}", "png")), withFigures, options);
        PdfRenderer.Render(One(), without, options);

        var pdf = Encoding.Latin1.GetString(File.ReadAllBytes(withFigures));
        Assert.That(pdf, Does.StartWith("%PDF"));
        Assert.That(pdf, Does.Contain("/Subtype /Image"));
        Assert.That(new FileInfo(withFigures).Length, Is.GreaterThan(new FileInfo(without).Length));
    }

    [Test]
    public void Pdf_fits_a_figure_taller_than_the_page()
    {
        var tall = """<svg xmlns="http://www.w3.org/2000/svg" width="3in" height="40in"><rect width="100%" height="100%" fill="#888"/></svg>""";
        var path = Temp.File("tall.pdf");
        Assert.DoesNotThrow(() => PdfRenderer.Render(One(new ImageBlock("data:image/svg+xml," + Uri.EscapeDataString(tall), "tall")),
            path, ExportOptions.ProseBook));
    }
}
