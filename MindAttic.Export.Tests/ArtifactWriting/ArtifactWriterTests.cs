using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MindAttic.Export.Artifacts;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.ArtifactWriting;

[TestFixture]
public class ArtifactWriterTests : TempDirTestBase
{
    private static readonly Regex Stamped = new(@"^(?<stem>.+)__\d{17}(?<ext>\.[^.]*)?$");

    private string Dir => Temp.Path;

    private string[] Files(string? dir = null) =>
        Directory.GetFiles(dir ?? Dir).Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();

    private string[] ArchiveFiles() =>
        Directory.Exists(Path.Combine(Dir, "Archives")) ? Files(Path.Combine(Dir, "Archives")) : [];

    private static ArtifactOptions With(ExistingArtifact existing, bool atomic = true) => new() { Existing = existing, Atomic = atomic };

    // ── content kinds ────────────────────────────────────────────────────────

    [Test]
    public async Task WriteText_is_utf8_without_bom_by_default()
    {
        var path = await ArtifactWriter.WriteTextAsync(Dir, "a.txt", "héllo — 🙂");
        Assert.That(path, Is.EqualTo(Path.Combine(Dir, "a.txt")));
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(new UTF8Encoding(false).GetBytes("héllo — 🙂")));
    }

    [Test]
    public async Task WriteText_with_bom_encoding_writes_the_preamble()
    {
        var path = await ArtifactWriter.WriteTextAsync(Dir, "a.txt", "x", new ArtifactOptions { Encoding = new UTF8Encoding(true) });
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'x' }));
    }

    [Test]
    public async Task WriteText_with_utf16_encoding()
    {
        var path = await ArtifactWriter.WriteTextAsync(Dir, "a.txt", "hi", new ArtifactOptions { Encoding = Encoding.Unicode });
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0 }));
    }

    [Test]
    public async Task WriteText_empty_content_creates_an_empty_file()
    {
        var path = await ArtifactWriter.WriteTextAsync(Dir, "empty.txt", "");
        Assert.That(new FileInfo(path).Length, Is.Zero);
    }

    public sealed record Sample(string Name, int Count, List<string> Tags);

    [Test]
    public async Task WriteJson_is_indented_by_default_and_round_trips()
    {
        var value = new Sample("x", 3, ["a", "b"]);
        var path = await ArtifactWriter.WriteJsonAsync(Dir, "s.json", value);
        var text = await File.ReadAllTextAsync(path);
        Assert.That(text, Does.Contain("\n"));
        Assert.That(text, Is.EqualTo(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })));
        var back = JsonSerializer.Deserialize<Sample>(text)!;
        Assert.That(back.Name, Is.EqualTo("x"));
        Assert.That(back.Tags, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public async Task WriteJson_with_custom_options()
    {
        var path = await ArtifactWriter.WriteJsonAsync(Dir, "s.json", new { A = 1 }, new JsonSerializerOptions());
        Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("{\"A\":1}"));
    }

    public static IEnumerable<TestCaseData> ByteSeeds() =>
        Enumerable.Range(0, 40).Select(s => new TestCaseData(s).SetName($"WriteBytes_round_trips(seed {s})"));

    [TestCaseSource(nameof(ByteSeeds))]
    public async Task WriteBytes_round_trips(int seed)
    {
        var r = new Random(seed);
        var bytes = new byte[r.Next(0, 200_000)];
        r.NextBytes(bytes);
        var path = await ArtifactWriter.WriteBytesAsync(Dir, $"blob{seed}.bin", bytes);
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Files(), Is.EqualTo(new[] { $"blob{seed}.bin" }), "no temp files left behind");
    }

    [Test]
    public async Task WriteStream_writes_what_the_delegate_produces()
    {
        var path = await ArtifactWriter.WriteStreamAsync(Dir, "s.bin", async (s, ct) =>
        {
            await s.WriteAsync(new byte[] { 1, 2, 3 }, ct);
            await s.WriteAsync(new byte[] { 4 }, ct);
        });
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public async Task WriteViaPath_moves_the_rendered_file_into_place()
    {
        string? renderedTo = null;
        var path = await ArtifactWriter.WriteViaPathAsync(Dir, "r.pdf", (p, _) =>
        {
            renderedTo = p;
            File.WriteAllText(p, "pdf");
            return Task.CompletedTask;
        });
        Assert.That(path, Is.EqualTo(Path.Combine(Dir, "r.pdf")));
        Assert.That(renderedTo, Is.Not.EqualTo(path), "rendered to a temporary path");
        Assert.That(Path.GetDirectoryName(renderedTo), Is.EqualTo(Dir));
        Assert.That(Path.GetFileName(renderedTo), Does.StartWith(".").And.EndWith(".pdf.tmp"));
        Assert.That(File.ReadAllText(path), Is.EqualTo("pdf"));
        Assert.That(Files(), Is.EqualTo(new[] { "r.pdf" }));
    }

    [Test]
    public async Task WriteViaPath_non_atomic_renders_straight_to_the_target()
    {
        string? renderedTo = null;
        var path = await ArtifactWriter.WriteViaPathAsync(Dir, "r.pdf", (p, _) => { renderedTo = p; File.WriteAllText(p, "x"); return Task.CompletedTask; },
            new ArtifactOptions { Atomic = false });
        Assert.That(renderedTo, Is.EqualTo(path));
    }

    [Test]
    public async Task Folder_is_created()
    {
        var nested = Path.Combine(Dir, "a", "b", "c");
        var path = await ArtifactWriter.WriteTextAsync(nested, "x.txt", "x");
        Assert.That(File.Exists(path), Is.True);
    }

    [TestCase("", "x.txt")]
    [TestCase("   ", "x.txt")]
    [TestCase("DIR", "")]
    [TestCase("DIR", "  ")]
    public void Blank_directory_or_name_throws(string dir, string name)
    {
        var d = dir == "DIR" ? Dir : dir;
        Assert.ThrowsAsync<ArgumentException>(() => ArtifactWriter.WriteTextAsync(d, name, "x"));
    }

    // ── atomicity ────────────────────────────────────────────────────────────

    [Test]
    public void Atomic_write_that_fails_leaves_no_temp_and_no_target()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => ArtifactWriter.WriteStreamAsync(Dir, "x.bin", async (s, ct) =>
        {
            await s.WriteAsync(new byte[] { 1 }, ct);
            throw new InvalidOperationException("boom");
        }));
        Assert.That(Files(), Is.Empty);
    }

    [Test]
    public async Task Atomic_overwrite_that_fails_keeps_the_original()
    {
        await File.WriteAllTextAsync(Path.Combine(Dir, "x.txt"), "original");
        Assert.ThrowsAsync<InvalidOperationException>(() => ArtifactWriter.WriteStreamAsync(Dir, "x.txt",
            (_, _) => throw new InvalidOperationException("boom"), With(ExistingArtifact.Overwrite)));
        Assert.That(Files(), Is.EqualTo(new[] { "x.txt" }));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "x.txt")), Is.EqualTo("original"));
    }

    [Test]
    public void Atomic_render_that_fails_leaves_no_temp()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => ArtifactWriter.WriteViaPathAsync(Dir, "x.docx", (p, _) =>
        {
            File.WriteAllText(p, "partial");
            throw new InvalidOperationException("boom");
        }));
        Assert.That(Files(), Is.Empty);
    }

    [Test]
    public void Non_atomic_write_that_fails_leaves_the_partial_file()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() => ArtifactWriter.WriteStreamAsync(Dir, "x.bin", async (s, ct) =>
        {
            await s.WriteAsync(new byte[] { 1 }, ct);
            throw new InvalidOperationException("boom");
        }, new ArtifactOptions { Atomic = false }));
        Assert.That(Files(), Is.EqualTo(new[] { "x.bin" }));
    }

    [Test]
    public async Task Many_atomic_writes_leave_no_temp_files()
    {
        for (var i = 0; i < 25; i++)
            await ArtifactWriter.WriteTextAsync(Dir, $"f{i % 5}.txt", $"v{i}", With(ExistingArtifact.Overwrite));
        Assert.That(Files(), Is.EqualTo(Enumerable.Range(0, 5).Select(i => $"f{i}.txt").ToArray()));
        Assert.That(Directory.GetFiles(Dir, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "f4.txt")), Is.EqualTo("v24"));
    }

    [Test]
    public void Cancellation_is_passed_to_the_writer()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => ArtifactWriter.WriteBytesAsync(Dir, "c.bin", new byte[10], ct: cts.Token));
        Assert.That(Directory.GetFiles(Dir, "*.tmp"), Is.Empty);
    }

    // ── existing-file policies ───────────────────────────────────────────────

    [Test]
    public async Task Archive_moves_the_existing_file_into_Archives_with_a_timestamp()
    {
        await ArtifactWriter.WriteTextAsync(Dir, "report.json", "one");
        await ArtifactWriter.WriteTextAsync(Dir, "report.json", "two");
        Assert.That(Files(), Is.EqualTo(new[] { "report.json" }));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "report.json")), Is.EqualTo("two"));
        var archived = ArchiveFiles().Single();
        var m = Stamped.Match(archived);
        Assert.That(m.Success, Is.True, archived);
        Assert.That(m.Groups["stem"].Value, Is.EqualTo("report"));
        Assert.That(m.Groups["ext"].Value, Is.EqualTo(".json"));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "Archives", archived)), Is.EqualTo("one"));
    }

    [Test]
    public async Task Archive_keeps_every_version_across_several_writes()
    {
        for (var i = 0; i < 4; i++)
        {
            await ArtifactWriter.WriteTextAsync(Dir, "log.txt", $"v{i}");
            await Task.Delay(5); // distinct millisecond stamps
        }
        Assert.That(ArchiveFiles(), Has.Length.EqualTo(3));
        var contents = ArchiveFiles().Select(f => File.ReadAllText(Path.Combine(Dir, "Archives", f))).OrderBy(x => x).ToArray();
        Assert.That(contents, Is.EqualTo(new[] { "v0", "v1", "v2" }));
    }

    [Test]
    public async Task Archive_never_fails_when_the_stamped_name_is_taken()
    {
        await ArtifactWriter.WriteTextAsync(Dir, "x.txt", "one");
        var archives = Path.Combine(Dir, "Archives");
        Directory.CreateDirectory(archives);
        var now = DateTime.UtcNow;
        for (var ms = -200; ms < 3000; ms++)
            File.WriteAllText(Path.Combine(archives, $"x__{now.AddMilliseconds(ms):yyyyMMddHHmmssfff}.txt"), "");
        Assert.DoesNotThrowAsync(() => ArtifactWriter.WriteTextAsync(Dir, "x.txt", "two"));
        Assert.That(File.ReadAllText(Path.Combine(Dir, "x.txt")), Is.EqualTo("two"));
    }

    [Test]
    public async Task ArchiveExisting_returns_the_archived_path()
    {
        var file = Path.Combine(Dir, "a.b.c.txt");
        await File.WriteAllTextAsync(file, "x");
        var archived = ArtifactWriter.ArchiveExisting(file);
        Assert.That(File.Exists(file), Is.False);
        Assert.That(File.Exists(archived), Is.True);
        Assert.That(Path.GetDirectoryName(archived), Is.EqualTo(Path.Combine(Dir, "Archives")));
        Assert.That(Stamped.Match(Path.GetFileName(archived)).Groups["stem"].Value, Is.EqualTo("a.b.c"));
    }

    [Test]
    public async Task Uniquify_numbers_new_copies_from_two()
    {
        var p1 = await ArtifactWriter.WriteTextAsync(Dir, "data.csv", "1", With(ExistingArtifact.Uniquify));
        var p2 = await ArtifactWriter.WriteTextAsync(Dir, "data.csv", "2", With(ExistingArtifact.Uniquify));
        var p3 = await ArtifactWriter.WriteTextAsync(Dir, "data.csv", "3", With(ExistingArtifact.Uniquify));
        Assert.That(new[] { p1, p2, p3 }.Select(p => Path.GetFileName(p)), Is.EqualTo(new[] { "data.csv", "data (2).csv", "data (3).csv" }));
        Assert.That(File.ReadAllText(p1), Is.EqualTo("1"));
        Assert.That(File.ReadAllText(p3), Is.EqualTo("3"));
        Assert.That(ArchiveFiles(), Is.Empty);
    }

    [Test]
    public async Task Uniquify_fills_the_first_gap()
    {
        await File.WriteAllTextAsync(Path.Combine(Dir, "x.txt"), "");
        await File.WriteAllTextAsync(Path.Combine(Dir, "x (3).txt"), "");
        var p = await ArtifactWriter.WriteTextAsync(Dir, "x.txt", "new", With(ExistingArtifact.Uniquify));
        Assert.That(Path.GetFileName(p), Is.EqualTo("x (2).txt"));
    }

    [Test]
    public async Task Uniquify_without_extension()
    {
        await ArtifactWriter.WriteTextAsync(Dir, "README", "1", With(ExistingArtifact.Uniquify));
        var p = await ArtifactWriter.WriteTextAsync(Dir, "README", "2", With(ExistingArtifact.Uniquify));
        Assert.That(Path.GetFileName(p), Is.EqualTo("README (2)"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Overwrite_replaces_in_place(bool atomic)
    {
        await ArtifactWriter.WriteTextAsync(Dir, "o.txt", "long original content", With(ExistingArtifact.Overwrite, atomic));
        await ArtifactWriter.WriteTextAsync(Dir, "o.txt", "short", With(ExistingArtifact.Overwrite, atomic));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "o.txt")), Is.EqualTo("short"));
        Assert.That(Files(), Is.EqualTo(new[] { "o.txt" }));
        Assert.That(ArchiveFiles(), Is.Empty);
    }

    [Test]
    public async Task Fail_throws_and_keeps_the_original()
    {
        await ArtifactWriter.WriteTextAsync(Dir, "f.txt", "original");
        var ex = Assert.ThrowsAsync<IOException>(() => ArtifactWriter.WriteTextAsync(Dir, "f.txt", "new", With(ExistingArtifact.Fail)));
        Assert.That(ex!.Message, Does.Contain("f.txt"));
        Assert.That(await File.ReadAllTextAsync(Path.Combine(Dir, "f.txt")), Is.EqualTo("original"));
    }

    [Test]
    public async Task Policies_apply_to_the_sanitized_name()
    {
        await ArtifactWriter.WriteTextAsync(Dir, "a:b.txt", "1");
        Assert.ThrowsAsync<IOException>(() => ArtifactWriter.WriteTextAsync(Dir, "a?b.txt", "2", With(ExistingArtifact.Fail)));
        Assert.That(Files(), Is.EqualTo(new[] { "ab.txt" }));
    }

    [Test]
    public async Task SanitizeName_false_uses_the_name_verbatim()
    {
        var p = await ArtifactWriter.WriteTextAsync(Dir, "CON-ok name..txt", "x", new ArtifactOptions { SanitizeName = false });
        Assert.That(Path.GetFileName(p), Is.EqualTo("CON-ok name..txt"));
    }

    // ── SanitizeFileName ─────────────────────────────────────────────────────

    [TestCase("report.json", "report.json")]
    [TestCase("KdpPublish-session-20261004-192103.autowebnav-recording.json", "KdpPublish-session-20261004-192103.autowebnav-recording.json")]
    [TestCase("archive.tar.gz", "archive.tar.gz")]
    [TestCase("v1.2.3.txt", "v1.2.3.txt")]
    [TestCase("a:b?c*d.json", "abcd.json")]
    [TestCase("x<y>z|w\"q.md", "xyzwq.md")]
    [TestCase("dir/sub\\file.txt", "dirsubfile.txt")]
    [TestCase("  padded.txt  ", "padded.txt")]
    [TestCase("trailing.", "trailing")]
    [TestCase("trailing. . ", "trailing")]
    [TestCase("...", "untitled")]
    [TestCase("", "untitled")]
    [TestCase("???", "untitled")]
    [TestCase("CON", "_CON")]
    [TestCase("con.json", "_con.json")]
    [TestCase("LPT1.log", "_LPT1.log")]
    [TestCase("COM¹.txt", "_COM¹.txt")]
    [TestCase("Console.txt", "Console.txt")]
    [TestCase("It's fine.txt", "It's fine.txt")]
    [TestCase("tab\there.txt", "tabhere.txt")]
    [TestCase("名誉 report 🙂.json", "名誉 report 🙂.json")]
    [TestCase("two  spaces.txt", "two  spaces.txt")]
    [TestCase(".gitignore", ".gitignore")]
    public void SanitizeFileName(string input, string expected) =>
        Assert.That(ArtifactWriter.SanitizeFileName(input), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> InvalidChars() =>
        Path.GetInvalidFileNameChars().Concat("\\/:*?\"<>|").Distinct().OrderBy(c => c)
            .Select(c => new TestCaseData(c).SetName($"SanitizeFileName_removes_U+{(int)c:X4}"));

    [TestCaseSource(nameof(InvalidChars))]
    public void SanitizeFileName_removes(char c) =>
        Assert.That(ArtifactWriter.SanitizeFileName($"re{c}port.v2.json"), Is.EqualTo("report.v2.json"));

    public static IEnumerable<TestCaseData> RandomNames() =>
        Enumerable.Range(0, 200).Select(s => new TestCaseData(s).SetName($"SanitizeFileName_result_is_writable(seed {s})"));

    [TestCaseSource(nameof(RandomNames))]
    public async Task SanitizeFileName_result_is_writable(int seed)
    {
        var g = new TextGen(seed);
        var name = g.MarkerSoup(20) + g.Pick(["", ".", ".txt", ".tar.gz", "  ", ":", "?"]) + g.Pick(["CON", "", "a"]);
        var safe = ArtifactWriter.SanitizeFileName(name);
        Assert.That(safe, Is.Not.Empty);
        Assert.That(ArtifactWriter.SanitizeFileName(safe), Is.EqualTo(safe), "idempotent");
        if (string.IsNullOrWhiteSpace(name))
        {
            Assert.ThrowsAsync<ArgumentException>(() => ArtifactWriter.WriteTextAsync(Dir, name, "x"));
            return;
        }
        var path = await ArtifactWriter.WriteTextAsync(Dir, name, "x", With(ExistingArtifact.Uniquify));
        Assert.That(File.Exists(path), Is.True, path);
        Assert.That(Path.GetDirectoryName(path), Is.EqualTo(Dir));
    }

    // ── ArtifactNames ────────────────────────────────────────────────────────

    [TestCase("KdpPublish-session", "json", "autowebnav-recording", "KdpPublish-session-20261004-192103.autowebnav-recording.json")]
    [TestCase("KdpPublish-session", ".json", "autowebnav-recording", "KdpPublish-session-20261004-192103.autowebnav-recording.json")]
    [TestCase("report", "md", null, "report-20261004-192103.md")]
    [TestCase("report", "..csv", null, "report-20261004-192103.csv")]
    [TestCase("x", "tar.gz", "k", "x-20261004-192103.k.tar.gz")]
    public void Timestamped(string prefix, string ext, string? kind, string expected) =>
        Assert.That(ArtifactNames.Timestamped(prefix, new DateTime(2026, 10, 4, 19, 21, 3, 999), ext, kind), Is.EqualTo(expected));

    public static IEnumerable<TestCaseData> Stamps() =>
        Enumerable.Range(0, 100).Select(s => new TestCaseData(s).SetName($"Timestamped_formats_any_date(seed {s})"));

    [TestCaseSource(nameof(Stamps))]
    public void Timestamped_formats_any_date(int seed)
    {
        var r = new Random(seed);
        var t = new DateTime(r.Next(1, 10000), r.Next(1, 13), r.Next(1, 29), r.Next(24), r.Next(60), r.Next(60));
        var name = ArtifactNames.Timestamped("p", t, "json");
        Assert.That(name, Is.EqualTo($"p-{t.Year:D4}{t.Month:D2}{t.Day:D2}-{t.Hour:D2}{t.Minute:D2}{t.Second:D2}.json"));
        Assert.That(ArtifactWriter.SanitizeFileName(name), Is.EqualTo(name));
    }

    [Test]
    public void Timestamped_ignores_current_culture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("th-TH"); // Buddhist calendar
            Assert.That(ArtifactNames.Timestamped("p", new DateTime(2026, 1, 2, 3, 4, 5), "txt"), Is.EqualTo("p-20260102-030405.txt"));
        }
        finally { Thread.CurrentThread.CurrentCulture = saved; }
    }
}
