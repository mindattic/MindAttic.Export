---
layer: bible
status: living
---
# MindAttic.Export — Project Bible

> Single source of truth for what MindAttic.Export IS, is NOT, and the rules that keep it coherent.
> README says how to build and run it; this says how to think about it. The finish line lives in
> `docs/RELEASE.md`.

## 1. The one sentence {#M.EXPT-§1}
MindAttic.Export is the one place a MindAttic application turns data into a user-facing file:
manuscripts and reports to `.docx`, `.epub`, `.pdf`, `.txt` and `.md`, and any other artifact
(JSON, CSV, HTML, recordings, zip bundles) through one atomic writer.

## 2. The product promise {#M.EXPT-§2}
- **Prose books come out exactly as they always did.** The book path (6"×9" KDP trim, Garamond) was
  migrated verbatim from Prose and is pinned byte-for-byte to the pre-migration renderer by the
  legacy-equivalence tests (`MindAttic.Export.Tests/Legacy*`).
- **Documents look like real academic work.** The document path (US Letter, page-numbered contents,
  headings, tables, lists, block quotes, LaTeX math, hanging-indent references, figures with APA
  captions) serves reports and the SyntheticExchangeTheory dissertation.
- **One call per bundle.** `ManuscriptExporter.ExportBundleAsync` writes `<Name> V<N>.{formats}` and
  archives the previous version instead of overwriting it.
- **Artifacts are written safely.** `ArtifactWriter` sanitises names, writes atomically, uses UTF-8
  without BOM by default and makes the caller choose what happens to an existing file.

## 3. What it is NOT {#M.EXPT-§3}
- **NOT an editor.** It reads Markdown or a built `Manuscript`; authoring lives in Prose and the apps.
- **NOT a downloader.** `DefaultImageResolver` reads paths, `file:` and `data:` URIs; it never fetches
  `http(s):` sources. Apps plug their own store in through `IImageResolver`.
- **NOT a place for silent book changes.** Anything that alters Prose book output needs the author's
  OK and an updated legacy oracle.

## 4. Architecture canon {#M.EXPT-§4}
```
 Markdown files / export.json      Manuscript model            Renderers
 ----------------------------      ----------------            ---------
 MarkdownManuscriptReader  ----->  Manuscript, Chapter,  --->  DocxRenderer  (OpenXml)
 mindattic-export CLI              blocks, glossary            PdfRenderer   (QuestPDF)
                                                               EpubRenderer
                                                               TextRenderer, MarkdownRenderer
 any app data  ----------------->  ArtifactWriter  ---------> atomic file, archive policy
```
### 4.1 Projects
- `MindAttic.Export` — the library, multi-targeted `net9.0` and `net10.0`, packed to `C:\LocalNuGet`.
- `MindAttic.Export.Cli` — `mindattic-export --config export.json`, same DI graph as the library.
- `MindAttic.Export.Tests` — NUnit, including the legacy-equivalence harness.

### 4.2 Domain model (the nouns)
`Manuscript`, `ManuscriptInfo`, `Chapter`; blocks: paragraph, sub-heading, heading, tenet, marker,
list, quote, table, math, rule, `ImageBlock`; glossary; `ExportOptions` (`Document`, book profiles);
`BundleRequest`, `ExportFormat`.

### 4.3 Key services (the verbs)
`MarkdownManuscriptReader.ReadFiles`, `ManuscriptExporter.ExportBundleAsync`, `ReportExporter`,
`ArtifactWriter.Write*Async`, `ExportArchive`, `ExportPaths`, `ProseInline` (the inline parser shared
with the Prose editor), `LatexMath`, `ImageLayout`.

## 5. The Laws {#M.EXPT-§5}
Inherits `MindAttic.HouseRules.md` by reference.
1. **Legacy equivalence.** {#M.EXPT-LAW-1} The book path stays byte-identical to Prose's
   pre-migration output unless the author approves a change and the oracle is updated with it.
2. **Archive, never delete.** {#M.EXPT-LAW-2} A new export version archives the old one.
3. **Whole-number versions.** {#M.EXPT-LAW-3} Every release bumps the major version.
4. **Pinned shared dependencies.** {#M.EXPT-LAW-4} `DocumentFormat.OpenXml`, `QuestPDF` and `Markdig`
   versions match Prose's so a consumer resolves one of each.
5. **CLI and library are one graph.** {#M.EXPT-LAW-5} The CLI registers the same DI graph as the
   library; it adds no rendering of its own.

## 6. Verified state {#M.EXPT-§6}
- 6.0.0 is on `C:\LocalNuGet`; Prose and KdpPublish consume it, ProcessLogger 5.0.0, eight apps 3.0.0
  (artifact writes only).
- Figures in DOCX and PDF with SVG kept vector and a pluggable resolver: f78cfec (5.2.0).
- Lyric stanzas and screenplay layout: cff683c (5.1.0). 16,580-test NUnit suite with the legacy
  harness: e502225 (4.0.0).

## 7. Active frontier {#M.EXPT-§7}
See `docs/RELEASE.md`: the 7.0 milestone (figure captions kept with their image, the book glossary
blank leaf, the 7.0.0 package and a clean dissertation re-export).

## 8. Quality bar {#M.EXPT-§8}
A renderer change is done when the legacy-equivalence tests pass (or the oracle was updated with the
author's OK), the affected format was opened and checked by eye, and the README describes it.

## 9. Glossary {#M.EXPT-§9}
- **Book path** — the Prose house style for KDP books.
- **Document path** — the US Letter profile for reports and academic work.
- **Tenet** — a Prose block rendered on its own page.
- **Bundle** — one versioned set of formats written by `ExportBundleAsync`.
- **Legacy oracle** — frozen copies of the original Prose renderer used by the equivalence tests.
