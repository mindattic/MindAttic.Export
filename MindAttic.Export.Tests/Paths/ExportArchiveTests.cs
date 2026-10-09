using System.Text.RegularExpressions;
using MindAttic.Export.Paths;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Paths;

[TestFixture]
public class ExportArchiveTests : TempDirTestBase
{
    private static readonly Regex StampSuffix = new(@"__\d{17}$");

    private string Dir => Temp.Path;

    private void Touch(string name, string content = "x") => File.WriteAllText(Path.Combine(Dir, name), content);

    private string[] Live() => Directory.GetFiles(Dir).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private string[] Archived(int v) =>
        Directory.Exists(Path.Combine(Dir, "Archives", $"v{v}"))
            ? Directory.GetFiles(Path.Combine(Dir, "Archives", $"v{v}")).Select(f => Path.GetFileName(f)).OrderBy(n => n, StringComparer.Ordinal).ToArray()
            : [];

    // ── ExtractVersion ───────────────────────────────────────────────────────

    [TestCase("1381 V13.docx", 13)]
    [TestCase("1381 v13.docx", 13)]
    [TestCase("SET V1.pdf", 1)]
    [TestCase("V7.md", 7)]
    [TestCase("V7", 7)]
    [TestCase("Book V12 final.txt", 12)]
    [TestCase("Book V0.epub", 0)]
    [TestCase("Book V007.epub", 7)]
    [TestCase("A V2 B V3.txt", 2)]
    [TestCase("Book V12345678.docx", 12345678)]
    [TestCase("Book\tV4.docx", 4)]
    public void ExtractVersion_finds_version(string name, int expected) =>
        Assert.That(ExportArchive.ExtractVersion(name), Is.EqualTo(expected));

    [TestCase("description.txt")]
    [TestCase("keywords.txt")]
    [TestCase("cover.jpg")]
    [TestCase("BookV13.docx")]
    [TestCase("Book V13x.docx")]
    [TestCase("Book V.docx")]
    [TestCase("Book VX.docx")]
    [TestCase("Book_V13.docx")]
    [TestCase("Version 2.docx")]
    [TestCase("")]
    [TestCase("Book V99999999999.docx")]
    public void ExtractVersion_none(string name) =>
        Assert.That(ExportArchive.ExtractVersion(name), Is.Null);

    public static IEnumerable<TestCaseData> VersionSeeds() =>
        Enumerable.Range(0, 300).Select(s => new TestCaseData(s).SetName($"ExtractVersion_from_bundle_names(seed {s})"));

    [TestCaseSource(nameof(VersionSeeds))]
    public void ExtractVersion_from_bundle_names(int seed)
    {
        var g = new TextGen(seed, TextFeatures.Plain);
        var v = g.R.Next(0, 100000);
        var baseName = g.Title(3).Replace("V", "W").Replace("v", "w");
        var ext = g.Pick(["docx", "epub", "pdf", "txt", "md"]);
        Assert.That(ExportArchive.ExtractVersion(ExportPaths.BundleFileName(baseName, v, ext)), Is.EqualTo(v));
    }

    // ── CurrentVersion ───────────────────────────────────────────────────────

    [Test]
    public void CurrentVersion_missing_folder_is_zero() =>
        Assert.That(ExportArchive.CurrentVersion(Path.Combine(Dir, "nope")), Is.Zero);

    [Test]
    public void CurrentVersion_empty_folder_is_zero() => Assert.That(ExportArchive.CurrentVersion(Dir), Is.Zero);

    [Test]
    public void CurrentVersion_reads_live_names()
    {
        Touch("SET V3.docx"); Touch("SET V12.pdf"); Touch("description.txt"); Touch("SET V9.md");
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(12));
    }

    [Test]
    public void CurrentVersion_reads_archive_folders()
    {
        Touch("SET V3.docx");
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "v17"));
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "V20"));
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "v20b"));
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "old"));
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(20));
    }

    [Test]
    public void CurrentVersion_ignores_files_inside_archives_and_nested_folders()
    {
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "v2"));
        File.WriteAllText(Path.Combine(Dir, "Archives", "v2", "SET V50.docx"), "x");
        Directory.CreateDirectory(Path.Combine(Dir, "Other"));
        File.WriteAllText(Path.Combine(Dir, "Other", "SET V60.docx"), "x");
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(2));
    }

    public static IEnumerable<TestCaseData> CurrentVersionSeeds() =>
        Enumerable.Range(0, 60).Select(s => new TestCaseData(s).SetName($"CurrentVersion_is_max_of_names_and_folders(seed {s})"));

    [TestCaseSource(nameof(CurrentVersionSeeds))]
    public void CurrentVersion_is_max_of_names_and_folders(int seed)
    {
        var r = new Random(seed);
        var max = 0;
        for (var i = r.Next(0, 5); i > 0; i--)
        {
            var v = r.Next(1, 500);
            Touch($"Book V{v}.{(i % 2 == 0 ? "docx" : "pdf")}");
            max = Math.Max(max, v);
        }
        for (var i = r.Next(0, 5); i > 0; i--)
        {
            var v = r.Next(1, 500);
            Directory.CreateDirectory(Path.Combine(Dir, "Archives", $"v{v}"));
            max = Math.Max(max, v);
        }
        Touch("description.txt");
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(max));
    }

    // ── Clean ────────────────────────────────────────────────────────────────

    [Test]
    public void Clean_moves_versioned_files_into_their_version_folder()
    {
        Touch("SET V3.docx", "docx3"); Touch("SET V3.pdf", "pdf3"); Touch("SET V2.md", "md2");
        ExportArchive.Clean(Dir);
        Assert.That(Live(), Is.Empty);
        Assert.That(Archived(3), Is.EqualTo(new[] { "SET V3.docx", "SET V3.pdf" }));
        Assert.That(Archived(2), Is.EqualTo(new[] { "SET V2.md" }));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", "v3", "SET V3.docx")), Is.EqualTo("docx3"));
    }

    [Test]
    public void Clean_uses_the_fallback_version_for_unversioned_files()
    {
        Touch("description.txt"); Touch("keywords.txt"); Touch("SET V4.txt");
        ExportArchive.Clean(Dir, fallbackVersion: 4);
        Assert.That(Archived(4), Is.EqualTo(new[] { "SET V4.txt", "description.txt", "keywords.txt" }));
        Assert.That(Live(), Is.Empty);
    }

    [Test]
    public void Clean_without_fallback_archives_unversioned_files_under_v0()
    {
        Touch("notes.txt");
        ExportArchive.Clean(Dir);
        Assert.That(Archived(0), Is.EqualTo(new[] { "notes.txt" }));
    }

    [TestCase("cover.jpg")]
    [TestCase("COVER.JPG")]
    [TestCase("Cover.Jpg")]
    public void Clean_keeps_the_cover_live_but_still_archives_a_copy(string cover)
    {
        Touch(cover, "img"); Touch("SET V1.docx");
        ExportArchive.Clean(Dir, 1);
        Assert.That(Live(), Is.EqualTo(new[] { cover }));
        Assert.That(Archived(1), Does.Contain(cover));
    }

    [Test]
    public void Clean_does_not_overwrite_an_existing_archive_copy()
    {
        Directory.CreateDirectory(Path.Combine(Dir, "Archives", "v5"));
        File.WriteAllText(Path.Combine(Dir, "Archives", "v5", "SET V5.docx"), "old");
        Touch("SET V5.docx", "new");
        ExportArchive.Clean(Dir);
        var files = Archived(5);
        Assert.That(files, Has.Length.EqualTo(2));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", "v5", "SET V5.docx")), Is.EqualTo("old"));
        var stamped = files.Single(f => f != "SET V5.docx");
        Assert.That(Path.GetExtension(stamped), Is.EqualTo(".docx"));
        Assert.That(StampSuffix.IsMatch(Path.GetFileNameWithoutExtension(stamped)), Is.True, stamped);
        Assert.That(stamped, Does.StartWith("SET V5__"));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", "v5", stamped)), Is.EqualTo("new"));
    }

    [Test]
    public void Clean_creates_a_missing_folder()
    {
        var nested = Path.Combine(Dir, "a", "b");
        ExportArchive.Clean(nested);
        Assert.That(Directory.Exists(nested), Is.True);
    }

    [Test]
    public void Clean_leaves_subfolders_alone()
    {
        Directory.CreateDirectory(Path.Combine(Dir, "Images"));
        File.WriteAllText(Path.Combine(Dir, "Images", "SET V1.png"), "x");
        ExportArchive.Clean(Dir);
        Assert.That(File.Exists(Path.Combine(Dir, "Images", "SET V1.png")), Is.True);
    }

    // ── ArchiveCurrent ───────────────────────────────────────────────────────

    [Test]
    public void ArchiveCurrent_copies_every_live_file_and_keeps_them_live()
    {
        Touch("SET V6.docx"); Touch("SET V6.pdf"); Touch("description.txt"); Touch("cover.jpg");
        ExportArchive.ArchiveCurrent(Dir, 6);
        Assert.That(Live(), Is.EqualTo(new[] { "SET V6.docx", "SET V6.pdf", "cover.jpg", "description.txt" }));
        Assert.That(Archived(6), Is.EqualTo(Live()));
    }

    [Test]
    public void ArchiveCurrent_uses_the_given_version_not_the_name()
    {
        Touch("SET V2.docx");
        ExportArchive.ArchiveCurrent(Dir, 9);
        Assert.That(Archived(9), Is.EqualTo(new[] { "SET V2.docx" }));
        Assert.That(Archived(2), Is.Empty);
    }

    [Test]
    public void ArchiveCurrent_twice_suffixes_the_second_copy()
    {
        Touch("SET V1.docx", "first");
        ExportArchive.ArchiveCurrent(Dir, 1);
        File.WriteAllText(Path.Combine(Dir, "SET V1.docx"), "second");
        ExportArchive.ArchiveCurrent(Dir, 1);
        var files = Archived(1);
        Assert.That(files, Has.Length.EqualTo(2));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", "v1", "SET V1.docx")), Is.EqualTo("first"));
        var stamped = files.Single(f => f != "SET V1.docx");
        Assert.That(StampSuffix.IsMatch(Path.GetFileNameWithoutExtension(stamped)), Is.True, stamped);
        Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", "v1", stamped)), Is.EqualTo("second"));
    }

    [Test]
    public void ArchiveCurrent_on_empty_folder_creates_the_version_folder()
    {
        ExportArchive.ArchiveCurrent(Dir, 3);
        Assert.That(Directory.Exists(Path.Combine(Dir, "Archives", "v3")), Is.True);
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(3));
    }

    [Test]
    public void Clean_then_ArchiveCurrent_full_cycle_never_deletes_anything()
    {
        for (var v = 1; v <= 4; v++)
        {
            ExportArchive.Clean(Dir, v - 1 == 0 ? null : v - 1);
            Touch($"SET V{v}.docx", $"d{v}"); Touch($"SET V{v}.md", $"m{v}"); Touch("description.txt", $"desc{v}");
            ExportArchive.ArchiveCurrent(Dir, v);
        }
        Assert.That(Live(), Is.EqualTo(new[] { "SET V4.docx", "SET V4.md", "description.txt" }));
        for (var v = 1; v <= 4; v++)
        {
            Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", $"v{v}", $"SET V{v}.docx")), Is.EqualTo($"d{v}"));
            Assert.That(File.ReadAllText(Path.Combine(Dir, "Archives", $"v{v}", "description.txt")), Is.EqualTo($"desc{v}"));
        }
        Assert.That(ExportArchive.CurrentVersion(Dir), Is.EqualTo(4));
    }
}
