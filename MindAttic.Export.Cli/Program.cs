using Microsoft.Extensions.DependencyInjection;
using MindAttic.Export;
using MindAttic.Export.Configuration;
using MindAttic.Export.Markdown;

// mindattic-export --config <export.json> [--out <dir>] [--formats docx,pdf,txt,md] [--version N] [--no-archive]
//
// Reads the Markdown sources named in the config, renders every requested format into a
// versioned bundle ("<Name> V<N>.<ext>"), archives the previous bundle (archive, never delete),
// and writes description.txt / keywords.txt.

string? configPath = null, outDir = null, formats = null;
int? version = null;
var archive = true;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--config" or "--out" or "--formats" or "--version" && i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"{args[i]} needs a value.");
        PrintUsage();
        return 2;
    }
    switch (args[i])
    {
        case "--config": configPath = args[++i]; break;
        case "--out": outDir = args[++i]; break;
        case "--formats": formats = args[++i]; break;
        case "--version":
            if (!int.TryParse(args[++i], out var v))
            {
                Console.Error.WriteLine($"--version needs a whole number, not '{args[i]}'.");
                PrintUsage();
                return 2;
            }
            version = v;
            break;
        case "--no-archive": archive = false; break;
        case "-h" or "--help":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 2;
    }
}

if (configPath is null)
{
    PrintUsage();
    return 2;
}

var services = new ServiceCollection().AddMindAtticExport().BuildServiceProvider();
var exporter = services.GetRequiredService<ManuscriptExporter>();

configPath = Path.GetFullPath(configPath);
var config = ExportConfig.Load(configPath);
var sources = config.ResolveSources(Path.GetDirectoryName(configPath)!);
if (sources.Count == 0)
{
    Console.Error.WriteLine("No source files matched.");
    return 1;
}

var manuscript = MarkdownManuscriptReader.ReadFiles(sources, config.ToInfo(), config.ToReaderOptions());
var request = new BundleRequest
{
    Directory = outDir ?? config.ResolveOutputDirectory(Path.GetDirectoryName(configPath)!),
    FileBaseName = config.FileBaseName,
    Formats = (formats?.Split(',') ?? [.. config.Formats]).Select(ExportFormatExtensions.Parse).ToList(),
    Version = version ?? config.Version,
    Archive = archive && config.Archive,
    Sidecars = config.Sidecars,
    Options = config.ToOptions()
};

var result = await exporter.ExportBundleAsync(manuscript, request);
Console.WriteLine($"[mindattic-export] {config.Title}: {sources.Count} source file(s), {manuscript.Chapters.Count} chapter(s), {result.WordCount:N0} words -> V{result.Version}");
foreach (var (format, path) in result.Files) Console.WriteLine($"  {format,-4} {path}");
if (result.DescriptionPath is not null) Console.WriteLine($"  desc {result.DescriptionPath}");
if (result.KeywordsPath is not null) Console.WriteLine($"  keys {result.KeywordsPath}");
return 0;

static void PrintUsage() => Console.WriteLine(
    "Usage: mindattic-export --config <export.json> [--out <dir>] [--formats docx,epub,pdf,txt,md] [--version N] [--no-archive]");
