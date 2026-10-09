# MindAttic.Export

The one place a MindAttic application turns data into a user-facing file.

- **Manuscripts:** books and documents rendered to `.docx`, `.epub`, `.pdf`, `.txt` and `.md`. The book path is the Prose house style (6"×9" KDP trim, Garamond), migrated verbatim from Prose and verified byte-for-byte against the whole GLMZ corpus. The document path (US Letter, page-numbered contents, headings, tables, lists, block quotes, LaTeX math, hanging-indent references) serves reports and academic work.
- **Reports:** a single Markdown report exported as `.md` plus `.txt` / `.docx` / `.pdf` / `.epub` renders (`ReportExporter`).
- **Artifacts:** any other file an app produces (JSON, CSV, HTML, recordings, zip bundles) written through `ArtifactWriter`: sanitized names, atomic writes, UTF-8 without BOM by default, and an explicit policy for an existing file (archive, uniquify, overwrite or fail).

Everything follows the MindAttic house rules: archive, never delete; whole-number versions; the CLI and the library register the same DI graph.

## Consumers

| Consumer | Uses |
|---|---|
| Prose | Book export (`--export-node`, `--preview`), book reports (`--export-book-report`), the shared `ProseInline` parser, path and archive helpers |
| SyntheticExchangeTheory | The dissertation (`export.json` → `mindattic-export`) |
| AutoWebNav, KdpPublish, Automata, JobHunt, Tutor, MindAttic.Ideas, IdiotProof, MediaButler, OpenCredentials | Artifact writes (recordings, data exports, bundles, reports) |

## Library

```csharp
using MindAttic.Export;
using MindAttic.Export.Markdown;

// A Markdown document to a versioned bundle: "<Name> V<N>.{docx,pdf,txt,md}".
var manuscript = MarkdownManuscriptReader.ReadFiles(files, new ManuscriptInfo { Title = "Synthetic Exchange Theory", Author = "Ryan DeBraal" });
var result = await new ManuscriptExporter().ExportBundleAsync(manuscript, new BundleRequest
{
    Directory = @"D:\Projects\MindAttic\ePub\MindAttic\SET",
    FileBaseName = "SET",
    Formats = [ExportFormat.Docx, ExportFormat.Pdf, ExportFormat.Txt, ExportFormat.Md],
    Options = ExportOptions.Document
});

// Any artifact, written atomically with a timestamped name.
await ArtifactWriter.WriteJsonAsync(downloads,
    ArtifactNames.Timestamped("KdpPublish-session", DateTime.Now, "json", kind: "autowebnav-recording"), recording,
    options: new ArtifactOptions { Existing = ExistingArtifact.Overwrite });
```

| Namespace | Contents |
|---|---|
| `MindAttic.Export.Model` | `Manuscript`, `Chapter`, blocks (paragraph, sub-heading, heading, tenet, marker, list, quote, table, math, rule), glossary |
| `MindAttic.Export.Renderers` | `DocxRenderer`, `EpubRenderer`, `PdfRenderer`, `TextRenderer`, `MarkdownRenderer` |
| `MindAttic.Export.Markdown` | `MarkdownManuscriptReader` (chapters, document blocks, math; report mode) |
| `MindAttic.Export.Text` | `ProseInline` (the inline-markup parser shared with the Prose editor), `LatexMath` |
| `MindAttic.Export.Paths` | `ExportPaths` (file-name sanitising, bundle names), `ExportArchive` (archive, never delete; versions) |
| `MindAttic.Export.Metadata` | `ReadingInfo` (description.txt reading-time line, keywords.txt) |
| `MindAttic.Export.Artifacts` | `ArtifactWriter`, `ArtifactOptions`, `ArtifactNames` |

## CLI

```
mindattic-export --config export.json [--out <dir>] [--formats docx,epub,pdf,txt,md] [--version N] [--no-archive]
```

`export.json` names the title page, the Markdown sources (wildcards read in ordinal order), the output folder, the formats and the page profile (`letter` or `trade`). See `SyntheticExchangeTheory/export.json` for a complete example.

## Build, test, pack

```
dotnet build
dotnet test
dotnet pack MindAttic.Export/MindAttic.Export.csproj -c Release -o artifacts
```

Consumers take the package through `C:\LocalNuGet` or their own `lib/local-packages` folder. Package versions are pinned to Prose's (`DocumentFormat.OpenXml` 3.3.0, `QuestPDF` 2026.5.0, `Markdig` 1.1.2) so a consumer resolves one version of each. Targets `net9.0` and `net10.0`.

## Verifying a renderer change

The book path must stay byte-identical to Prose's pre-migration output:

1. The legacy-equivalence tests (`MindAttic.Export.Tests/Legacy*`) render hundreds of generated manuscripts with frozen copies of the original Prose code and with the library, and compare.
2. Against real books: `prose --export-node --id <book> --preview <dir>` for every book before and after the change, then compare the two folders (Markdown, PDF and text byte-for-byte; docx and epub by part content, since OpenXml mints random part names and relationship ids on every save).

## License

Not yet chosen.
