---
codex: 1
project: MindAttic.Export
code: M.EXPT
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# MindAttic.Export — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| M.EXPT-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| M.EXPT-T2 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| M.EXPT-T3 | 2026-10-16 | chore | closed | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | 6054a7f; pass 4 clean |
| M.EXPT-T4 | 2026-10-16 | chore | wont-do | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | portfolio reset: repo-sweep test loop; tests run only on explicit owner request |
| M.EXPT-T5 | 2026-10-16 | chore | wont-do | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | portfolio reset: generic cleanup, audit nice-to-have with no concrete defect |
| M.EXPT-T6 | 2026-10-16 | docs | closed | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | already done: README.md current (figures, resolver, Storage naming: f78cfec, 1f4e2c5) |
| M.EXPT-T7 | 2026-10-16 | feature | wont-do | Log to MindAttic.Log (3.0.0): every app/CLI entry point writes to the standalone MindAttic.Log.db Vault assigns this app (VaultPaths.LoggingDbPath, MindAttic.Vault 6.0.0) through Serilog's WriteToMindAtticLog, tagged with its Component; log startup, user actions that change data, failures and unhandled exceptions (UI thread, AppDomain, unobserved tasks); tests point the log at a temp file. Pattern: MindAttic.Workflow WorkflowLog (M.FLOW-T92), MindAttic.Orchestrator OrchestratorLog | portfolio reset: MindAttic.Log adoption is outside the Export 7.0 milestone |
| M.EXPT-T8 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T9 | 2026-10-19 | chore | wont-do | Convert tracked source files to LF line endings via .gitattributes, per HOUSE-LAW-13 (low priority) | — |
| M.EXPT-T10 | 2026-10-20 | docs | closed | Publish MindAttic.Export docs to GitHub Pages via the shared Agency DocFX pipeline: run adopt-docs-site.ps1 (README.md as home page, docs/*.md as articles, API reference only where the code builds in CI), enable Pages with build_type=workflow, set the repo homepage URL, retire tools/build-readme.ps1 and README.htm, confirm the docs Actions run is green and https://mindattic.github.io/MindAttic.Export/ returns 200. Depends on MAT-T8. | — |
| M.EXPT-T11 | 2026-10-21 | fix | closed | MarkdownManuscriptReader drops nested list items: a list item keeps only its direct paragraphs, so '- a' with a nested '  - b' exports without 'b' in every format | ab19b9c |
| M.EXPT-T12 | 2026-10-21 | fix | closed | MarkdownRenderer double-escapes table pipes: a cell read from Markdown as 'a \\| b' is written back as 'a \\\| b', which splits the cell and breaks the table | 6814ccd |
| M.EXPT-T13 | 2026-10-21 | fix | closed | mindattic-export CLI crashes with an unhandled exception on a non-numeric --version or a flag missing its value, instead of printing usage and exiting 2 | d48a6d1 |
| M.EXPT-T14 | 2026-10-21 | feature | wont-do | Decide: render nested Markdown lists as real nested lists (model + docx/epub/pdf/txt/md renderers) instead of the current fold into the parent item ('a; b; c') that M.EXPT-T11 introduced to stop sub-items being lost | portfolio reset: nested-list renderer overhaul is speculative; the T11 fold keeps content |
| M.EXPT-T15 | 2026-10-21 | fix | closed | Docx export crashes on save (ArgumentException) and EPUB writes unparseable XHTML when text holds a lone UTF-16 surrogate or U+FFFE/U+FFFF; extend the XML-illegal scrub in DocxRenderer and EpubRenderer to strip them | b186a96 |
| M.EXPT-T16 | 2026-10-21 | fix | closed | Docx renders a code block (or reference entry) inside a Markdown blockquote as a justified quote paragraph, so the code's line breaks collapse into one line; render non-body paragraphs in a quote with their own role | abef900 |
| M.EXPT-T17 | 2026-10-21 | fix | closed | EPUB writes options.BookIdentifier into dc:identifier and Manuscript.Language into xml:lang attributes unescaped, so an identifier with & or < (e.g. a URL) or a language with a quote makes content.opf/XHTML unparseable | 9cd500e |
| M.EXPT-T18 | 2026-10-21 | fix | closed | LatexMath.Group drops the last character of an unclosed {group} (x^{2 renders as x, \sqrt{xy as sqrt(x)); keep the whole remainder when the closing brace is missing | a37b307 |
| M.EXPT-T19 | 2026-10-21 | fix | closed | LatexMath writes the argument of \mathbf/\mathrm/\mathit/\operatorname verbatim, so a nested command prints as source (\mathbf{\mu} renders as the text \mu); convert math-mode font arguments and keep only \text-family arguments verbatim | f444de9 |
| M.EXPT-T20 | 2026-10-21 | fix | closed | LatexMath reads \sqrt[n]{x} as \sqrt applied to the token '[', printing the index and brackets inside the radical (sqrt([)3]x); parse the optional index and set it as a superscript before the radical | 737b2a3 |
| M.EXPT-T21 | 2026-10-21 | fix | closed | LatexMath prints a stray '.' for the null delimiter in \left. / \right. (common in piecewise and evaluated-at expressions); the period after \left or \right means no delimiter | 6affd57 |
| M.EXPT-T22 | 2026-10-21 | fix | closed | MarkdownManuscriptReader silently drops every list-item child that is not a paragraph or nested list (fenced/indented code, blockquote, table, display math inside a list item); split the list around such blocks so they are kept in reading order | 4a21ad5 |
| M.EXPT-T23 | 2026-10-21 | fix | closed | PdfRenderer throws on a TableBlock with no rows (InvalidOperationException: Sequence contains no elements, from the column-weight Max) or whose rows have no cells (QuestPDF: table needs a column); skip a table with no cells, as docx/epub/txt render it without error | ccba113 |
| M.EXPT-T24 | 2026-10-21 | fix | closed | LatexMath leaves a fraction's numerator/denominator unbracketed when it holds a multiplication or plus-minus sign, so \frac{a}{b \cdot c} prints a/b·c (reads as (a/b)·c); treat ·, ×, ÷, ±, ∓ as compound so the term is parenthesised | 6054a7f |
| M.EXPT-T25 | 2026-10-21 | feature | open | A docx book whose final chapter ends on a tenet page and that has a glossary prints a blank leaf before the Glossary (the tenet's closing page break plus the glossary's own break). Fix it; this changes Prose book output pinned by the legacy-equivalence rule, so get the author's OK and update the legacy oracle in the same change | — |
| M.EXPT-T26 | 2026-10-21 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T27 | 2026-10-22 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T28 | 2026-10-24 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T29 | 2026-11-02 | docs | closed | Adopt MindAttic.Agency docs-site pipeline, publish README to GitHub Pages (HOUSE-LAW-14) | — |
| M.EXPT-T30 | 2026-11-02 | fix | closed | Bughunt sweep, two passes: find and fix real bugs across tracked source (user request 2026-11-02) | — |
| M.EXPT-T31 | 2026-11-02 | feature | closed | Support images (SVG with PNG fallback) in DOCX and PDF export; exporter currently drops them silently (user request 2026-11-02) | — |
| M.EXPT-T32 | 2026-11-02 | feature | closed | Carry Markdown images through every renderer: ImageBlock from ![alt](path) with APA figure captions; SVG in PDF (QuestPDF .Svg), SVG plus PNG fallback in DOCX, img in EPUB, pass-through in MD, placeholder in TXT (needed by SET-T32, the dissertation's 23 figures) | children done |
| M.EXPT-T33 | 2026-11-02 | fix | closed | Fix the open items the 2026-11-02 bughunt left for decision (one-chapter docs keep their heading in .docx, empty EPUB toc gets a valid entry) | — |
| M.EXPT-T34 | 2026-11-03 | fix | open | Figure captions split from their image at page breaks: keep the bold 'Figure N' label and italic title paragraphs with the following figure-only paragraph in PDF (QuestPDF ShowEntire/unbreakable column) and DOCX (KeepNext on the caption paragraphs). Seen in SET V22 p.10-11 and p.72-73, and in SET V32 for Figures 1, 6, 8, 11, 12, 13, 15, 16, 21, 22 (e.g. PDF p.45/46; folded in from M.EXPT-T47) | — |
| M.EXPT-T35 | 2026-11-10 | chore | closed | Crash recovery: commit the command-catalog sync (.agent/commands/*.md) and regenerated docs/USER_STORIES.md left uncommitted by the session crash | — |
| M.EXPT-T36 | 2026-11-10 | chore | closed | Adopt the shared docs-site pipeline: turn on GitHub Pages | — |
| M.EXPT-T37 | 2026-11-16 | feature | closed | ExportArchive: let the caller choose where archived versions live (archiveDir param), so Prose can archive to a sibling tree instead of nesting under the live bundle folder | fad02f7 |
| M.EXPT-T38 | 2026-11-17 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T39 | 2026-11-22 | feature | closed | Add ImageBlock to the model and parse ![alt](path) Markdown into it with APA figure caption (bold 'Figure N' label, italic title) in MindAttic.Export | already done: f78cfec (ImageBlock model + Markdown parsing, M.EXPT-T31) |
| M.EXPT-T40 | 2026-11-22 | feature | closed | Render ImageBlock in the PDF renderer, with SVG via QuestPDF .Svg and the figure caption | already done: f78cfec (PDF renders ImageBlock, SVG via QuestPDF, M.EXPT-T31) |
| M.EXPT-T41 | 2026-11-22 | feature | closed | Render ImageBlock in the DOCX renderer: SVG plus PNG fallback, with figure caption | already done: f78cfec (DOCX renders ImageBlock, SVG + PNG fallback, M.EXPT-T31) |
| M.EXPT-T42 | 2026-11-22 | feature | closed | Render ImageBlock in EPUB as img, pass through in MD, and emit a placeholder in TXT | already done: f78cfec (EPUB/TXT print [Figure: alt], MD writes the image back, M.EXPT-T31) |
| M.EXPT-T43 | 2026-11-22 | docs | closed | 2026-11-22 morning | — |
| M.EXPT-T44 | 2026-11-24 | fix | wont-do | Hunt the whole repo for real bugs with no time limit and fix every confirmed one | portfolio reset: open-ended bughunt with no concrete defect |
| M.EXPT-T45 | 2026-11-24 | chore | closed | MindAttic.Export docs and comments that name MindAttic.Media say MindAttic.Storage | children done |
| M.EXPT-T46 | 2026-11-24 | chore | closed | MindAttic.Export/ExportOptions.cs (~56), MindAttic.Export/Images/ExportImages.cs (~45) and README.md (~56) say MindAttic.Storage instead of MindAttic.Media | 1f4e2c5 |
| M.EXPT-T47 | 2026-11-26 | fix | wont-do | PDF export separates a figure's APA caption (Figure N + title paragraphs) from its image: in Dissertation.SyntheticExchangeTheory SET V32 the caption sits alone at the foot of the page and the figure starts the next page for Figures 1, 6, 8, 11, 12, 13, 15, 16, 21, 22 (e.g. PDF p.45/46); the caption paragraphs directly above a figure-only paragraph should keep with next | duplicate of M.EXPT-T34 |
| M.EXPT-T48 | 2026-11-27 | chore | open | Portfolio reset (MAT-T324): release charter (docs/RELEASE.md, one milestone), bible, and triage of every open ticket into epic>feature>task for: MindAttic.Export, MindAttic.Log, MindAttic.Helpers, MindAttic.Storage, MindAttic.Telemetry, MindAttic.Browser, MindAttic.Web | — |
| M.EXPT-T49 | 2026-11-27 | feature | open | Figures and their captions lay out as one unit, and the known book blank-leaf defect is fixed (portfolio reset milestone, M.EXPT-T48) | — |
| M.EXPT-T50 | 2026-11-27 | feature | open | The bold Figure N label and italic title stay on the same page as the image they caption, in PDF and DOCX | — |
| M.EXPT-T51 | 2026-11-27 | feature | open | A Prose book ending on a tenet page with a glossary prints no blank leaf before the Glossary | — |
| M.EXPT-T52 | 2026-11-27 | feature | open | Ship the figure-complete renderer as MindAttic.Export 7.0.0 and prove it on the dissertation (portfolio reset, M.EXPT-T48) | — |
| M.EXPT-T53 | 2026-11-27 | feature | open | MindAttic.Export 7.0.0 packed to C:\LocalNuGet with the README describing the changes | — |
| M.EXPT-T54 | 2026-11-27 | chore | open | Bump MindAttic.Export (and the CLI) to 7.0.0 after M.EXPT-T34 and M.EXPT-T25 land, pack to C:\LocalNuGet, and note the caption keep-with-figure and glossary blank-leaf changes in README.md | — |
| M.EXPT-T55 | 2026-11-27 | feature | open | The SET dissertation re-exported with 7.0.0 keeps every figure with its caption | — |
| M.EXPT-T56 | 2026-11-27 | chore | open | Re-export Dissertation.SyntheticExchangeTheory (export.json) with MindAttic.Export 7.0.0 and confirm in the PDF and DOCX that Figures 1, 6, 8, 11, 12, 13, 15, 16, 21 and 22 keep their Figure N label and title on the same page as the image (the pages M.EXPT-T47 cited); file a fix ticket for any that still split | — |
