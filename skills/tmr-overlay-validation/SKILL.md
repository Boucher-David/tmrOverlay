---
name: tmr-overlay-validation
description: Use when validating tmrOverlay changes, especially before handing code back from a non-Windows machine or when .NET build/test cannot run locally. Consolidates repo validation from build.md, README.md, CI, and local static checks for C# compile-shape hazards.
---

# TmrOverlay Validation

Use this skill before finalizing code changes in this repo. Start with the checks that match the files changed, then report anything that could not run on the current machine.

## Always Run

```bash
git diff --check
```

```bash
rg -n "^(<<<<<<<|>>>>>>>|=======)$" --glob '!captures/**' --glob '!captures-latest/**' --glob '!local-mac/TmrOverlayMac/.build/**' --glob '!**/*.png' --glob '!**/*.jpg' --glob '!**/*.bin'
```

## C# Compile-Shape Check

When any `.cs` file changes, run the local scanner before relying on Windows-only build feedback:

```bash
python3 skills/tmr-overlay-validation/scripts/check-csharp-member-duplicates.py
```

This is a broad static pass for type-local C# name and scope hazards that compile catches but plain source review often misses. It should look for generated or implicit members colliding with explicitly declared members, and for primary-constructor signatures that reference declarations that are only available inside the type body.

Examples of the kind of issue this should catch:

- A positional record parameter creates `SkiesLabel`, then the same record declares a helper method named `SkiesLabel`.
- A nested record primary constructor takes `ActivityBadge`, but `ActivityBadge` is declared inside that nested record body, outside primary-constructor scope.
- A field, property, event, nested type, or primary-constructor generated property reuses a member name in the same type in a way that is not ordinary method overloads.

When editing snapshots/read models, prefer helper names that describe the operation, such as `FormatSkiesLabel` or `DetermineDeclaredWetSurfaceMismatch`, instead of names identical to public generated properties.

## Windows .NET Gate

When `dotnet` is available, use the same production gate as CI:

```powershell
dotnet restore .\tmrOverlay.sln
dotnet build .\tmrOverlay.sln --configuration Release --no-restore
dotnet test .\tmrOverlay.sln --configuration Release --no-build
```

For quick app-focused checks during Windows development, `build.md` also lists:

```powershell
dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
dotnet test .\tmrOverlay.sln
```

If `dotnet` is not available locally, state that clearly. The local scanner is a fallback, not a replacement for Windows build/test verification.

## Python Tools

When touching `tools/analysis/*.py`, run:

```bash
env PYTHONPYCACHEPREFIX=/tmp/tmr-pycache python3 -m py_compile tools/analysis/synthesize_capture.py tools/analysis/export_history_from_capture.py tools/analysis/yaml_forensics.py
```

## macOS Harness

When touching `local-mac/TmrOverlayMac/`, run from that directory when the local toolchain supports it:

```bash
swift build
```

```bash
swift test
```

The current local Command Line Tools setup may build but fail to run XCTest; report that limitation if it applies.

## Change-Specific Checks

- Keep public snapshot member names stable unless the requested change intentionally updates the contract.
- If changing Windows build/release commands, keep `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`, `build.md`, and `docs/windows-dotnet-commands.md` aligned. Copied release package commands should either create their required artifact directories themselves or state a clear precondition before `Compress-Archive`.
- If changing raw capture format, verify `docs/capture-format.md`, `telemetry.md`, and `README.md` were updated in the same pass.
- If changing IBT capture or analysis behavior, verify `docs/ibt-analysis.md`, `docs/capture-format.md`, `telemetry.md`, and `README.md` stay aligned. IBT analysis must remain a compact sidecar: no source `.ibt` copy by default, bounded candidate scanning, file-size and stability checks, timeout/sample limits, and skipped/failed status files instead of failures in compact history, post-race analysis, or capture synthesis.
- If materially changing overlays, update or call out missing review images under `mocks/<overlay-id>/`.
- If changing shared contracts, reusable overlay behavior, or app boilerplate, inspect whether both the Windows app and ignored mac harness need matching changes.
