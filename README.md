# rename-ranger

Batch file renamer for Windows — build powerful rename rules with a live preview, apply them safely across hundreds of files, and undo in one click. Regex/find-replace, sequential numbering, case transforms, and metadata tokens (EXIF date, file dates), with optional local-AI smart naming. **Offline & privacy-first** — nothing leaves your machine.

## Overview

`rename-ranger` is a small, focused Windows 10/11 desktop utility for renaming files in bulk. You drop in a folder (or drag files), stack up a set of rename rules, and see exactly what every file will be renamed to *before* you commit. A single undo restores the previous names using a saved operation journal.

It works entirely offline. An optional local-AI mode can suggest human-friendly names from file content/metadata using tiny models via Ollama or any llama.cpp OpenAI-compatible endpoint — but the tool is fully useful with AI turned off.

## Motivation

Renaming a pile of files on Windows is painful: File Explorer's built-in bulk rename only does `name (1), name (2)`, PowerShell one-liners are error-prone and unpreviewable, and most third-party tools are cluttered, adware-laden, or cloud-tied. `rename-ranger` gives you a clean, previewable, reversible batch rename with the power features (regex, tokens, numbering) — and keeps everything local.

## Use cases

- **Photographers:** rename `IMG_1234.JPG` → `2026-08-01_Vacation_001.jpg` using EXIF capture date + a sequence.
- **Downloads cleanup:** strip `[website.com]`, normalize spaces/underscores, Title Case a folder of messy filenames.
- **Developers:** regex-rename test fixtures, prefix/suffix files, zero-pad numbers (`img1` → `img001`).
- **Music/media:** reorder `Track - Artist` to `Artist - Track`, remove junk tags.
- **Documents:** append modified-date tokens, enforce a naming convention across a project folder.

## How to use (Windows-first quickstart)

1. Download the latest portable build from **Releases** (`rename-ranger-win-x64.zip`) and unzip, or install the MSIX.
2. Launch `RenameRanger.exe`.
3. Drag a folder or files into the window (or use **Add Files…**).
4. Add one or more **rules** from the toolbar (Find & Replace, Regex, Insert/Remove, Numbering, Case, Metadata Token…).
5. Watch the **live preview** column update — conflicts and invalid names are flagged in red.
6. Click **Apply**. If anything looks wrong afterward, click **Undo** to roll back.

### Example workflow

Rename a folder of vacation photos:

1. Rule 1 — **Metadata Token:** pattern `{exif:date:yyyy-MM-dd}_Vacation`
2. Rule 2 — **Numbering:** suffix `_###` starting at 1, step 1
3. Rule 3 — **Extension:** lowercase

Preview:

```
IMG_1234.JPG  ->  2026-08-01_Vacation_001.jpg
IMG_1235.JPG  ->  2026-08-01_Vacation_002.jpg
IMG_1236.JPG  ->  2026-08-01_Vacation_003.jpg
```

Click **Apply**. Done — reversible via **Undo**.

### Rule types (planned)

| Rule | What it does |
|------|--------------|
| Find & Replace | Literal text substitution (optional case-sensitive) |
| Regex | Pattern match with capture-group backreferences (`$1`) |
| Insert / Remove | Insert text at position; remove range or by pattern |
| Numbering | Sequential counter with start/step/zero-pad, prefix/suffix |
| Case | UPPER / lower / Title / Sentence case |
| Metadata Token | `{exif:date}`, `{file:modified}`, `{file:created}`, `{size}`, `{ext}` |
| Trim / Clean | Collapse whitespace, strip bracketed tags, normalize separators |

## Local-AI integration (optional)

When enabled in Settings, `rename-ranger` can call a **local** small model to propose descriptive names — e.g. reading a document's first lines or an image's caption to suggest `Invoice_AcmeCorp_2026-Q2` instead of `scan0042`.

- Works with **Ollama** or any **llama.cpp** server exposing an OpenAI-compatible `/v1/chat/completions` endpoint.
- Settings include a local-AI enable toggle, endpoint URL, and model name, persisted to `%APPDATA%\rename-ranger\settings.json`.
- Recommended tiny models: **MiniCPM-family**, `qwen2.5:1.5b`, `llama3.2:1b`, or similar small instruct/vision models.
- The AI endpoint is **probed for reachability**; if unavailable, the app silently falls back to rule-based naming.
- **Off by default.** No cloud services, no telemetry, no network required for core features.

## Current status / milestones

🚧 **Bootstrapping.** Docs and issue backlog are up; implementation is issue-by-issue.

- [ ] M1 — Core rename engine + rule pipeline (`RenameRanger.Core`, unit-tested)
- [ ] M2 — WPF UI: file list, rule stack, live preview
- [ ] M3 — Safe apply + undo journal
- [ ] M4 — Metadata tokens (EXIF/file dates) + numbering/case rules
- [ ] M5 — Optional local-AI smart naming
- [ ] M6 — Windows packaging (portable zip + MSIX)

See [PLAN.md](./PLAN.md) for scope, architecture, and non-goals.
