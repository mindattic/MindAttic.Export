---
layer: release
status: living
---
# MindAttic.Export — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
The shared library and `mindattic-export` CLI that turn MindAttic data into user-facing files: Prose books and
academic documents to docx/epub/pdf/txt/md, reports, and atomic artifact writes for a dozen apps.

## 2. The milestone: MindAttic.Export 7.0 {#milestone}
- **Milestone:** MS-42, "MindAttic.Export 7.0: figure-complete documents"
- **Published means:** `MindAttic.Export.7.0.0.nupkg` on `C:\LocalNuGet` (a finished internal library).
- **The natural stopping point:** figures shipped in 5.2.0, but their APA captions can still fall on the page
  before the image, and Prose books with a final tenet and a glossary print a blank leaf. With both fixed, packed
  as 7.0.0 and proven on the dissertation, every document path the consumers use is complete.

## 3. Done when (acceptance checks)
1. build — `MindAttic.Export.slnx` builds clean on main.
2. exists — `C:\LocalNuGet\MindAttic.Export.7.0.0.nupkg`.
3. signoff — the owner opens the re-exported SET PDF and DOCX: every figure sits with its caption.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | M.EXPT-T49 Figure layout fidelity | Captions stay with figures; book blank leaf fixed | M.EXPT-T50 Caption keeps with figure (990: M.EXPT-T34), M.EXPT-T51 Book glossary blank leaf (980: M.EXPT-T25) |
| 800 | M.EXPT-T52 7.0 release | 7.0.0 packed and proven on the dissertation | M.EXPT-T53 Package 7.0.0 (890: M.EXPT-T54), M.EXPT-T55 Dissertation verification (880: M.EXPT-T56) |

## 5. Out of scope (not before this milestone)
- Nested Markdown lists rendered as real nested lists — the fold from M.EXPT-T11 keeps the content.
- Logging the CLI to MindAttic.Log — portfolio-wide adoption, not this library's finish line.
- Moving the eight apps on 3.0.0 (artifact writes only) forward — their own repos' work.
- Open-ended bughunts, generic cleanups and repo-sweep test runs.

## 6. Current verified state
- 6.0.0 on `C:\LocalNuGet`; Prose and KdpPublish consume it.
- Figures (ImageBlock, SVG in PDF/DOCX, pluggable resolver, EPUB/TXT/MD handling): f78cfec.
- README current with figures and the Storage rename: 1f4e2c5.

## 7. Decision record
- 2026-11-27 — charter drafted from README, git log and tickets; Legion vote: **Approve**, 9/9 voters, plurality
  quorum reached, consensus 1.0, no dissent.
- Triage: 15 open → done 6 (T32, T39–T42, T6), duplicate 1 (T47 → T34), wont-do 5 (T4, T5, T7, T14, T44),
  kept 2 (T34, T25), new tasks 2 (T54, T56). M.EXPT-T48 is the triage job itself.
