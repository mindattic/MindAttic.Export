using Microsoft.Extensions.DependencyInjection;
using MindAttic.Export.Metadata;
using MindAttic.Export.Model;
using MindAttic.Export.Renderers;
using MindAttic.Export.Tests.Support;
using MindAttic.Export.Text;

namespace MindAttic.Export.Tests.Export;

[TestFixture]
public class ManuscriptExporterTests : TempDirTestBase
{
    private static readonly ExportOptions Fixed = ExportOptions.ProseBook with
    {
        BookIdentifier = "urn:uuid:00000000-0000-0000-0000-000000000001",
        FixedTimestamp = new DateTime(2026, 10, 4, 19, 21, 3, DateTimeKind.Utc)
    };

    private static Manuscript Book(string? description = "A blurb.", List<string>? keywords = null) => new()
    {
        Title = "The Book",
        Author = "MindAttic",
        Description = description,
        Keywords = keywords ?? ["one", "two"],
        Chapters =
        [
            new Chapter("Chapter 1", [new ParagraphBlock("It was *dark* and **cold**."), new ParagraphBlock("Second para here.")]),
            new Chapter("Chapter 2", [new ParagraphBlock("More words follow now.")])
        ]
    };

    private string Dir => Temp.Sub("bundle");

    private BundleRequest Request(params ExportFormat[] formats) => new()
    {
        Directory = Dir,
        FileBaseName = "BOOK",
        Formats = formats.Length == 0 ? new BundleRequest { Directory = "x", FileBaseName = "x" }.Formats : formats,
        Options = Fixed
    };

    private string[] Live() => Directory.GetFiles(Dir).Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();

    [Test]
    public async Task First_bundle_is_version_one_with_every_format_and_sidecars()
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request());
        Assert.That(result.Version, Is.EqualTo(1));
        Assert.That(result.Files.Keys, Is.EquivalentTo(Enum.GetValues<ExportFormat>()));
        Assert.That(Live(), Is.EqualTo(new[] { "BOOK V1.docx", "BOOK V1.epub", "BOOK V1.md", "BOOK V1.pdf", "BOOK V1.txt", "description.txt", "keywords.txt" }));
        foreach (var (format, path) in result.Files)
        {
            Assert.That(Path.GetFileName(path), Is.EqualTo($"BOOK V1.{format.Extension()}"));
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
        }
        Assert.That(result.DescriptionPath, Is.EqualTo(Path.Combine(Dir, "description.txt")));
        Assert.That(result.KeywordsPath, Is.EqualTo(Path.Combine(Dir, "keywords.txt")));
        Assert.That(result.WordCount, Is.EqualTo(ManuscriptExporter.WordCount(Book())));
        Assert.That(File.ReadAllText(result.DescriptionPath!), Is.EqualTo(ReadingInfo.WithReadingLine("A blurb.", result.WordCount)));
        Assert.That(File.ReadAllLines(result.KeywordsPath!), Is.EqualTo(new[] { "one", "two" }));
        // Archived copy of the new bundle.
        Assert.That(Directory.GetFiles(Path.Combine(Dir, "Archives", "v1")), Has.Length.EqualTo(7));
    }

    [Test]
    public async Task Versions_increment_and_previous_bundles_are_archived()
    {
        var exporter = new ManuscriptExporter();
        for (var v = 1; v <= 3; v++)
        {
            var result = await exporter.ExportBundleAsync(Book(), Request(ExportFormat.Txt, ExportFormat.Md));
            Assert.That(result.Version, Is.EqualTo(v));
        }
        Assert.That(Live(), Is.EqualTo(new[] { "BOOK V3.md", "BOOK V3.txt", "description.txt", "keywords.txt" }));
        for (var v = 1; v <= 3; v++)
        {
            var archive = Path.Combine(Dir, "Archives", $"v{v}");
            Assert.That(File.Exists(Path.Combine(archive, $"BOOK V{v}.txt")), Is.True, archive);
            Assert.That(File.Exists(Path.Combine(archive, $"BOOK V{v}.md")), Is.True, archive);
            Assert.That(File.Exists(Path.Combine(archive, "description.txt")), Is.True, archive);
        }
    }

    [Test]
    public async Task Explicit_version_is_used()
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request(ExportFormat.Txt) with { Version = 42 });
        Assert.That(result.Version, Is.EqualTo(42));
        Assert.That(Live(), Does.Contain("BOOK V42.txt"));
        Assert.That(Directory.Exists(Path.Combine(Dir, "Archives", "v42")), Is.True);
    }

    [Test]
    public async Task Version_continues_from_archive_folders()
    {
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "v9"));
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request(ExportFormat.Md));
        Assert.That(result.Version, Is.EqualTo(10));
    }

    [Test]
    public async Task No_archive_leaves_previous_files_live_and_creates_no_archive()
    {
        var exporter = new ManuscriptExporter();
        await exporter.ExportBundleAsync(Book(), Request(ExportFormat.Txt) with { Archive = false });
        await exporter.ExportBundleAsync(Book(), Request(ExportFormat.Txt) with { Archive = false });
        Assert.That(Live(), Is.EqualTo(new[] { "BOOK V1.txt", "BOOK V2.txt", "description.txt", "keywords.txt" }));
        Assert.That(Directory.Exists(Path.Combine(Dir, "Archives")), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Description_sidecar_only_when_there_is_a_description(string? description)
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(description), Request(ExportFormat.Txt));
        Assert.That(result.DescriptionPath, Is.Null);
        Assert.That(File.Exists(Path.Combine(Dir, "description.txt")), Is.False);
    }

    [Test]
    public async Task Keywords_sidecar_only_when_there_are_keywords()
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(keywords: []), Request(ExportFormat.Txt));
        Assert.That(result.KeywordsPath, Is.Null);
        Assert.That(File.Exists(Path.Combine(Dir, "keywords.txt")), Is.False);
        Assert.That(result.DescriptionPath, Is.Not.Null);
    }

    [Test]
    public async Task Sidecars_off_writes_neither()
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request(ExportFormat.Txt) with { Sidecars = false });
        Assert.That((result.DescriptionPath, result.KeywordsPath), Is.EqualTo(((string?)null, (string?)null)));
        Assert.That(Live(), Is.EqualTo(new[] { "BOOK V1.txt" }));
    }

    [Test]
    public async Task Description_reading_line_is_not_duplicated_on_re_export()
    {
        var withLine = ReadingInfo.WithReadingLine("Blurb.", 99999);
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(withLine), Request(ExportFormat.Txt));
        var text = File.ReadAllText(result.DescriptionPath!);
        Assert.That(text.Split("Approximately").Length - 1, Is.EqualTo(1));
        Assert.That(text, Does.StartWith("Blurb.\n\n"));
    }

    public static IEnumerable<TestCaseData> FormatSubsets()
    {
        var all = Enum.GetValues<ExportFormat>();
        for (var mask = 1; mask < 1 << all.Length; mask++)
        {
            var subset = all.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
            yield return new TestCaseData((object)subset).SetName($"Formats_subset_writes_exactly_those({string.Join("+", subset)})");
        }
    }

    [TestCaseSource(nameof(FormatSubsets))]
    public async Task Formats_subset_writes_exactly_those(ExportFormat[] formats)
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request(formats) with { Sidecars = false });
        Assert.That(result.Files.Keys, Is.EquivalentTo(formats));
        Assert.That(Live(), Is.EquivalentTo(formats.Select(f => $"BOOK V1.{f.Extension()}")));
    }

    [Test]
    public async Task Duplicate_formats_are_rendered_once()
    {
        var result = await new ManuscriptExporter().ExportBundleAsync(Book(), Request(ExportFormat.Md, ExportFormat.Md, ExportFormat.Txt) with { Sidecars = false });
        Assert.That(result.Files, Has.Count.EqualTo(2));
    }

    [Test]
    public void Unregistered_renderer_throws()
    {
        var exporter = new ManuscriptExporter([new TextRenderer()]);
        Assert.That(exporter.Renderer(ExportFormat.Txt), Is.TypeOf<TextRenderer>());
        Assert.Throws<NotSupportedException>(() => exporter.Renderer(ExportFormat.Pdf));
        Assert.ThrowsAsync<NotSupportedException>(() => exporter.ExportBundleAsync(Book(), Request(ExportFormat.Pdf)));
    }

    [Test]
    public void DefaultRenderers_cover_every_format_once()
    {
        var formats = ManuscriptExporter.DefaultRenderers().Select(r => r.Format).ToList();
        Assert.That(formats, Is.EquivalentTo(Enum.GetValues<ExportFormat>()));
    }

    [Test]
    public void Service_registration_matches_the_default_graph()
    {
        using var provider = new ServiceCollection().AddMindAtticExport().BuildServiceProvider();
        var renderers = provider.GetServices<IManuscriptRenderer>().ToList();
        Assert.That(renderers.Select(r => r.GetType()), Is.EquivalentTo(ManuscriptExporter.DefaultRenderers().Select(r => r.GetType())));
        var exporter = provider.GetRequiredService<ManuscriptExporter>();
        Assert.That(provider.GetRequiredService<ManuscriptExporter>(), Is.SameAs(exporter));
        foreach (var format in Enum.GetValues<ExportFormat>())
            Assert.That(exporter.Renderer(format).Format, Is.EqualTo(format));
    }

    [Test]
    public async Task Same_options_give_identical_txt_md_and_epub_content()
    {
        var a = await new ManuscriptExporter().ExportBundleAsync(Book(), Request() with { Directory = Temp.Sub("a"), Sidecars = false });
        var b = await new ManuscriptExporter().ExportBundleAsync(Book(), Request() with { Directory = Temp.Sub("b"), Sidecars = false });
        foreach (var f in new[] { ExportFormat.Txt, ExportFormat.Md })
            Assert.That(File.ReadAllBytes(a.Files[f]), Is.EqualTo(File.ReadAllBytes(b.Files[f])));
        var ea = Packages.ReadZip(a.Files[ExportFormat.Epub]);
        var eb = Packages.ReadZip(b.Files[ExportFormat.Epub]);
        Assert.That(ea.Select(e => (e.Name, e.Text)), Is.EqualTo(eb.Select(e => (e.Name, e.Text))));
    }

    // ── WordCount ────────────────────────────────────────────────────────────

    [Test]
    public void WordCount_counts_running_text_of_every_block_kind()
    {
        var m = new Manuscript
        {
            Title = "T",
            Chapters =
            [
                new Chapter("Ignored heading words", [
                    new ParagraphBlock("one **two**"),                                           // 2
                    new HeadingBlock(2, "three four"),                                            // 2
                    new SubHeadingBlock("five"),                                                  // 1
                    new TenetBlock("礼\nRei"),                                                    // 2
                    new ListBlock(false, 1, [new ParagraphBlock("six"), new ParagraphBlock("seven eight")]), // 3
                    new QuoteBlock([new ParagraphBlock("nine"), new ListBlock(true, 1, [new ParagraphBlock("ten")])]), // 2
                    new TableBlock([new TableRow(true, [new ParagraphBlock("eleven"), new ParagraphBlock("twelve")])]), // 2
                    new MathBlock("x", [new ProseInline.Span("x", ProseInline.Style.Italic)]),   // 0
                    new RuleBlock(),
                    new MarkerBlock("beat:1:abc")
                ])
            ],
            Glossary = [new GlossaryEntry("Ignored", null, "glossary words ignored")]
        };
        Assert.That(ManuscriptExporter.WordCount(m), Is.EqualTo(14));
    }
}
