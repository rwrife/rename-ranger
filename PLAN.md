# rename-ranger — Project Plan

## Scope

A focused Windows 10/11 desktop utility that renames files in bulk via a stackable, previewable, reversible rule pipeline.

**In scope:**
- Add files/folders (drag-drop + dialog), recursive folder option.
- Stackable rename rules: Find & Replace, Regex (with backreferences), Insert/Remove, Sequential Numbering (start/step/zero-pad), Case transforms, Trim/Clean, Metadata Tokens (EXIF date, file created/modified dates, size, extension).
- **Live preview** of old → new names with conflict/invalid-name detection.
- **Safe apply** with collision handling (two-phase rename via temp names) and an **undo journal** to roll back the last operation.
- Optional **local-AI smart naming** via Ollama / llama.cpp OpenAI-compatible endpoint (off by default, graceful fallback).
- Persistent settings + saved rule presets under `%APPDATA%\rename-ranger`.

**Out of scope (non-goals):**
- No cloud sync, accounts, or telemetry.
- No moving/copying files across folders (rename only, in place).
- No mass content editing — filenames only.
- No cross-platform GUI in v1 (Windows-first; Core stays portable .NET).
- No scheduled/watched auto-rename daemon in v1.

## Architecture / tech approach

- **Language/runtime:** C# on **.NET 8**.
- **UI:** **WPF** (`RenameRanger.App`) — `DataGrid` for the file/preview list, a rule-stack panel, toolbar. MVVM.
- **Core:** `RenameRanger.Core` — UI-free class library holding the rule engine, rule types, preview computation, collision detection, and the undo journal. Fully unit-testable.
- **Rule pipeline:** each rule implements `IRenameRule.Apply(RenameContext) -> string`. Rules run in order; preview = run pipeline over all items without touching disk.
- **Metadata:** `MetadataExtractor` NuGet for EXIF; `System.IO.FileInfo` for file dates/size.
- **Apply/undo:** operations written to a JSON journal (`%APPDATA%\rename-ranger\journal\`). Two-phase rename (to temp names then final) avoids A→B/B→A collisions. Undo replays the journal in reverse.
- **Local-AI:** `RenameRanger.Core.Ai` — thin `HttpClient` client for OpenAI-compatible `/v1/chat/completions`; reachability probe with short timeout; feature flag in settings; any failure → fall back to rule output.
- **Settings/presets:** JSON under `%APPDATA%\rename-ranger`.
- **Tests:** **xUnit** on `RenameRanger.Core` (rule correctness, numbering/zero-pad, regex backrefs, collision handling, journal round-trip, token substitution).

### Solution layout

```
rename-ranger.sln
  src/RenameRanger.Core/      # rule engine, tokens, journal, AI client (no UI deps)
  src/RenameRanger.App/       # WPF app
  tests/RenameRanger.Core.Tests/  # xUnit
```

## Milestones

- **M1 — Core engine:** rule interface, Find/Replace + Regex + Insert/Remove + Case rules, preview computation, xUnit tests.
- **M2 — WPF shell:** file list DataGrid, rule stack UI, drag-drop, live preview binding.
- **M3 — Safe apply + undo:** two-phase rename, collision/invalid-name detection, JSON journal, undo.
- **M4 — Numbering + metadata tokens:** sequential numbering with zero-pad, EXIF/file-date/size/ext tokens, trim/clean rule.
- **M5 — Local-AI smart naming:** settings toggle, endpoint config + probe, suggestion flow, graceful fallback.
- **M6 — Packaging & release:** portable self-contained x64 zip + MSIX, README quickstart, first tagged release.

## Packaging target for Windows

- **Primary:** portable, self-contained single-folder **win-x64** build (`dotnet publish -r win-x64 --self-contained`) zipped as `rename-ranger-win-x64.zip`.
- **Secondary:** **MSIX** package for double-click install / Start-menu integration.
- Distributed via GitHub **Releases**. No installer telemetry, no runtime prerequisites for the self-contained build.
