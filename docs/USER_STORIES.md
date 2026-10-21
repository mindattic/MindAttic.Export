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
| M.EXPT-T4 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| M.EXPT-T5 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| M.EXPT-T6 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| M.EXPT-T7 | 2026-10-16 | feature | open | Log to MindAttic.Log (3.0.0): every app/CLI entry point writes to the standalone MindAttic.Log.db Vault assigns this app (VaultPaths.LoggingDbPath, MindAttic.Vault 6.0.0) through Serilog's WriteToMindAtticLog, tagged with its Component; log startup, user actions that change data, failures and unhandled exceptions (UI thread, AppDomain, unobserved tasks); tests point the log at a temp file. Pattern: MindAttic.Workflow WorkflowLog (M.FLOW-T92), MindAttic.Orchestrator OrchestratorLog | M.FLOW-T92 |
| M.EXPT-T8 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
| M.EXPT-T9 | 2026-10-19 | chore | open | Convert tracked source files to LF line endings via .gitattributes, per HOUSE-LAW-13 (low priority) | — |
| M.EXPT-T10 | 2026-10-20 | docs | open | Publish MindAttic.Export docs to GitHub Pages via the shared Toolkit DocFX pipeline: run adopt-docs-site.ps1 (README.md as home page, docs/*.md as articles, API reference only where the code builds in CI), enable Pages with build_type=workflow, set the repo homepage URL, retire tools/build-readme.ps1 and README.htm, confirm the docs Actions run is green and https://mindattic.github.io/MindAttic.Export/ returns 200. Depends on MAT-T8. | — |
| M.EXPT-T11 | 2026-10-21 | fix | closed | MarkdownManuscriptReader drops nested list items: a list item keeps only its direct paragraphs, so '- a' with a nested '  - b' exports without 'b' in every format | ab19b9c |
| M.EXPT-T12 | 2026-10-21 | fix | closed | MarkdownRenderer double-escapes table pipes: a cell read from Markdown as 'a \\| b' is written back as 'a \\\| b', which splits the cell and breaks the table | 6814ccd |
| M.EXPT-T13 | 2026-10-21 | fix | closed | mindattic-export CLI crashes with an unhandled exception on a non-numeric --version or a flag missing its value, instead of printing usage and exiting 2 | d48a6d1 |
| M.EXPT-T14 | 2026-10-21 | feature | open | Decide: render nested Markdown lists as real nested lists (model + docx/epub/pdf/txt/md renderers) instead of the current fold into the parent item ('a; b; c') that M.EXPT-T11 introduced to stop sub-items being lost | — |
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
| M.EXPT-T25 | 2026-10-21 | feature | open | Decide: a docx book whose final chapter ends on a tenet page and that has a glossary prints a blank leaf before the Glossary (the tenet's closing page break plus the glossary's own break). Fixing it changes Prose book output, which the legacy-equivalence rule pins to the pre-migration renderer, so it needs the author's OK (and the legacy oracle updated) | — |
| M.EXPT-T26 | 2026-10-21 | docs | closed | Update USER_STORIES.md | — |
