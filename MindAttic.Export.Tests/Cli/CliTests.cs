using System.Diagnostics;
using MindAttic.Export.Tests.Support;

namespace MindAttic.Export.Tests.Cli;

/// <summary>
/// Runs the built CLI (<c>mindattic-export.dll</c>, built ahead of this project through a
/// non-referencing ProjectReference) in a child <c>dotnet</c> process against a temp config.
/// </summary>
[TestFixture]
[NonParallelizable]
public class CliTests : TempDirTestBase
{
    private static string CliDll()
    {
        // …\MindAttic.Export.Tests\bin\<Configuration>\<tfm>\ → …\MindAttic.Export.Cli\bin\<Configuration>\<tfm>\
        var testDir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        var tfm = testDir.Name;
        var configuration = testDir.Parent!.Name;
        var solution = testDir.Parent!.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(solution, "MindAttic.Export.Cli", "bin", configuration, tfm, "mindattic-export.dll");
        Assert.That(File.Exists(dll), Is.True, $"CLI not built: {dll}");
        return dll;
    }

    private sealed record Run(int ExitCode, string Out, string Err);

    private static Run Cli(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(CliDll());
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(180_000))
        {
            p.Kill(entireProcessTree: true);
            Assert.Fail("CLI timed out");
        }
        p.WaitForExit();
        return new Run(p.ExitCode, stdout.Result, stderr.Result);
    }

    private string Out => Temp.Sub("out");

    private string Config(string? outputDirectory = null, string formats = "\"docx\", \"epub\", \"pdf\", \"txt\", \"md\"", string sources = "\"chapters/*.md\", \"refs.md\"")
    {
        Directory.CreateDirectory(Temp.Sub("chapters"));
        File.WriteAllText(Temp.Sub("chapters", "02-two.md"), "# Chapter 2: Markets\n\nPrices $p_t$ move.\n\n| a | b |\n|---|---|\n| 1 | 2 |\n");
        File.WriteAllText(Temp.Sub("chapters", "01-one.md"), "Preface text.\n\n# Chapter 1: Origins\n\nIt *began* with **one** idea.\n\n- first\n- second\n");
        File.WriteAllText(Temp.File("refs.md"), "# References\n\nSmith, J. (2020). *Title*.\n");
        var json = $$"""
            {
              // test config
              "title": "Synthetic Exchange Theory",
              "subtitle": "A Test",
              "author": "MindAttic",
              "description": "A dissertation.",
              "keywords": ["economics", "theory"],
              "sources": [{{sources}}],
              "outputDirectory": {{System.Text.Json.JsonSerializer.Serialize(outputDirectory ?? Out)}},
              "fileBaseName": "SET",
              "formats": [{{formats}}],
            }
            """;
        var path = Temp.File("export.json");
        File.WriteAllText(path, json);
        return path;
    }

    private string[] Live() => Directory.GetFiles(Out).Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();

    [Test]
    public void Exports_a_full_bundle_then_the_next_version()
    {
        var config = Config();
        var first = Cli(Temp.Path, "--config", config);
        Assert.That(first.ExitCode, Is.EqualTo(0), first.Err + first.Out);
        Assert.That(first.Out, Does.Contain("[mindattic-export] Synthetic Exchange Theory: 3 source file(s), 4 chapter(s)"));
        Assert.That(first.Out, Does.Contain("-> V1"));
        Assert.That(Live(), Is.EqualTo(new[] { "SET V1.docx", "SET V1.epub", "SET V1.md", "SET V1.pdf", "SET V1.txt", "description.txt", "keywords.txt" }));
        var txt = File.ReadAllText(Path.Combine(Out, "SET V1.txt"));
        Assert.That(txt.IndexOf("Chapter 1 — Origins", StringComparison.Ordinal), Is.LessThan(txt.IndexOf("Chapter 2 — Markets", StringComparison.Ordinal)));
        Assert.That(txt, Does.Contain("Preface text."));
        Assert.That(File.ReadAllText(Path.Combine(Out, "description.txt")), Does.StartWith("A dissertation.").And.Contain("to read."));
        Assert.That(File.ReadAllLines(Path.Combine(Out, "keywords.txt")), Is.EqualTo(new[] { "economics", "theory" }));
        Structural.StructuralValidityTests.AssertOnlyKnownDefects(Structural.StructuralValidityTests.Validate(Path.Combine(Out, "SET V1.docx")));
        Assert.That(Support.Pdf.LooksValid(File.ReadAllBytes(Path.Combine(Out, "SET V1.pdf"))), Is.True);

        var second = Cli(Temp.Path, "--config", config);
        Assert.That(second.ExitCode, Is.EqualTo(0), second.Err);
        Assert.That(second.Out, Does.Contain("-> V2"));
        Assert.That(Live(), Does.Contain("SET V2.docx").And.Not.Contain("SET V1.docx"));
        Assert.That(File.Exists(Path.Combine(Out, "Archives", "v1", "SET V1.docx")), Is.True);
        Assert.That(File.Exists(Path.Combine(Out, "Archives", "v2", "SET V2.pdf")), Is.True);
    }

    [Test]
    public void Command_line_overrides_formats_version_output_and_archive()
    {
        var config = Config();
        var other = Temp.Sub("elsewhere");
        var run = Cli(Temp.Path, "--config", config, "--out", other, "--formats", "md,TXT,.markdown", "--version", "13", "--no-archive");
        Assert.That(run.ExitCode, Is.EqualTo(0), run.Err);
        var files = Directory.GetFiles(other).Select(f => Path.GetFileName(f)).OrderBy(f => f, StringComparer.Ordinal);
        Assert.That(files, Is.EqualTo(new[] { "SET V13.md", "SET V13.txt", "description.txt", "keywords.txt" }));
        Assert.That(Directory.Exists(Path.Combine(other, "Archives")), Is.False);
        Assert.That(Directory.Exists(Out), Is.False);
    }

    [Test]
    public void Help_prints_usage()
    {
        var run = Cli(Temp.Path, "--help");
        Assert.That(run.ExitCode, Is.EqualTo(0));
        Assert.That(run.Out, Does.StartWith("Usage: mindattic-export --config"));
    }

    [Test]
    public void Missing_config_argument_is_a_usage_error()
    {
        var run = Cli(Temp.Path);
        Assert.That(run.ExitCode, Is.EqualTo(2));
        Assert.That(run.Out, Does.Contain("Usage:"));
    }

    [Test]
    public void Unknown_argument_is_a_usage_error()
    {
        var run = Cli(Temp.Path, "--bogus");
        Assert.That(run.ExitCode, Is.EqualTo(2));
        Assert.That(run.Err, Does.Contain("Unknown argument: --bogus"));
    }

    [Test]
    public void No_matching_sources_exits_with_one()
    {
        var config = Config(sources: "\"chapters/*.nothing\"");
        var run = Cli(Temp.Path, "--config", config);
        Assert.That(run.ExitCode, Is.EqualTo(1));
        Assert.That(run.Err, Does.Contain("No source files matched."));
        Assert.That(Directory.Exists(Out), Is.False);
    }

    [Test]
    public void Unknown_format_fails_without_writing()
    {
        var run = Cli(Temp.Path, "--config", Config(), "--formats", "docx,mobi");
        Assert.That(run.ExitCode, Is.Not.EqualTo(0));
        Assert.That(run.Err, Does.Contain("Unknown export format 'mobi'"));
        Assert.That(Directory.Exists(Out), Is.False);
    }

    [Test]
    public void Relative_output_directory_resolves_against_the_config_folder()
    {
        var config = Config(outputDirectory: "out");
        var cwd = Temp.Sub("cwd");
        Directory.CreateDirectory(cwd);
        var run = Cli(cwd, "--config", config, "--formats", "md");
        Assert.That(run.ExitCode, Is.EqualTo(0), run.Err);
        Assert.That(File.Exists(Path.Combine(Out, "SET V1.md")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(cwd, "out")), Is.False);
    }
}
