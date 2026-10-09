# AGENTS.md: MindAttic.Export

Shared workflow: read [`../MINDATTIC_AGENT.md`](../MINDATTIC_AGENT.md) and `../mindattic-agent-standard/AGENTS.md`. This file adds project context only.

## Purpose

The one place a MindAttic application turns data into a user-facing file: book manuscripts (docx, epub, pdf, txt, md), Markdown documents (reports, the SET dissertation), and generic artifacts (JSON/CSV/HTML reports, recordings, bundles) through `ArtifactWriter`.

## Rules

- Prose book output must stay byte-identical to the pre-migration Prose renderers. The legacy-equivalence tests in `MindAttic.Export.Tests/Legacy` guard this; never change a book-path renderer without them passing.
- Package versions are pinned to Prose's (`DocumentFormat.OpenXml` 3.3.0, `QuestPDF` 2026.5.0, `Markdig` 1.1.2) so consumers resolve one version.
- Whole-number package versions (HOUSE-LAW-1). Archive, never delete (HOUSE-LAW-2). CLI and library register the identical DI graph (HOUSE-LAW-6). Done means clean build + green tests (HOUSE-LAW-8). Commit to main (HOUSE-LAW-10).
- Consumers take the package through their `lib/local-packages` folder (see Prose `NuGet.config`).
