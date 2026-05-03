---
name: tmr-overlay-validation
description: Use when validating tmrOverlay changes, especially before handing code back from a non-Windows machine or when .NET build/test cannot run locally. Consolidates repo validation from AGENTS.md, README.md, CI, docs, and local static checks for C# compile-shape hazards.
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

## Branch-Complete Release Hygiene

Run this section when a branch is being declared complete, prepared for merge, or promoted to a product milestone.

Make branch-readiness artifacts current before writing the final squash text:

- Run a stale-reference sweep across docs, mocks, tests, the ignored mac harness, repo skills, and release notes for old behavior names, settings tabs, option labels, source labels, screenshot assumptions, and API call patterns that no longer match the branch implementation.
- Patch affected behavior docs as part of this branch-complete pass. Overlay behavior changes should update `docs/overlay-logic.md` and the matching overlay-specific logic note. App/settings/platform changes should update README/current-state docs when the product shape changed. Product direction, validation assumptions, analysis assumptions, live-model contract notes, and future-branch notes should land here unless the change is a durable raw-capture/schema change that requires same-pass docs.
- If changing Windows build/release commands, make `README.md`, release-command docs, CI docs, and any in-app command references agree during this branch-complete pass.
- For overlay or settings UI changes, regenerate the deterministic mac-harness screenshots under `mocks/`, keep the contact sheet plus per-state PNGs current, and run `python3 tools/validate_overlay_screenshots.py`.
- Treat screenshot changes as validation artifacts. If screenshots cannot be regenerated on the current machine, say so explicitly and do not describe the branch as fully ready without that gap.
- Re-run `git diff --name-status "$(git merge-base main HEAD)"..HEAD` after docs and screenshots are current so `VERSION.md` and the final handoff text describe the actual final branch contents.

Inspect the branch shape and the text that GitHub is likely to expose in a PR squash:

```bash
git fetch origin --tags
git status --short --branch
git log --reverse --oneline "$(git merge-base main HEAD)"..HEAD
git diff --name-status "$(git merge-base main HEAD)"..HEAD
git tag --list --sort=version:refname -n2
```

Sanitize the first branch commit / squash text before handoff. The first commit message or planned PR squash title should not be a throwaway message such as "more changes", "fix", "done", "one diagnostic", "Even more overlays", or an accidental-main note. It should be versioned, product-facing, and specific enough to explain the branch without relying on private conversation context.

For branch-complete handoff:

- Update `VERSION.md` with the target version, suggested squash title, and squash body.
- Ensure the suggested title uses `vMAJOR.MINOR.PATCH`, for example `[v0.7.0] Add simple model-v2 overlays and design-v2 preview states`.
- Ensure the body summarizes the actual branch diff, including product behavior, data/schema/diagnostic changes, docs/validation artifacts, CI/release-pipeline changes, and any user-data compatibility impact.
- Re-read the final branch commit list and `git diff --name-status` after late fixes. If CI, validation, version metadata, build metadata, or follow-up bug fixes were added after the original release note was drafted, update `VERSION.md` again before final handoff.
- Keep the final answer's recommended squash title/body identical to the current `VERSION.md` suggestion unless explicitly calling out a requested alternative.
- Remove or generalize sensitive local details from the squash text: raw local paths, private diagnostic file names, uncommitted capture names, accidental branch-operation notes, and private conversation shorthand.
- Ensure `Directory.Build.props` `VersionPrefix` matches the branch target version when the branch is intended to produce that app milestone.
- Do not create the release tag on an unmerged feature branch unless the user explicitly declares that branch as the release point.

For a milestone already on `main`, create and push an annotated tag after confirming the release commit:

```bash
git tag -a vX.Y.Z <commit> -m "vX.Y.Z - Short release title" -m "Short release summary."
git push origin vX.Y.Z
```

Use `git push --force-with-lease` only for an explicitly approved repair of an accidental main push, and preserve the work on a branch before moving `main`.

## Local C# Compile-Shape Check

When any `.cs` file changes, run the local scanner. This is the main C# validation available on machines without `dotnet`:

```bash
python3 skills/tmr-overlay-validation/scripts/check-csharp-member-duplicates.py
```

This is a broad static pass for C# compile-shape hazards that compile catches but plain source review often misses. It should look for generated or implicit members colliding with explicitly declared members, primary-constructor signatures that reference declarations that are only available inside the type body, targetless nullable conditional expressions, and known external enum member typos for third-party APIs used by the app.

Examples of the kind of issue this should catch:

- A positional record parameter creates `SkiesLabel`, then the same record declares a helper method named `SkiesLabel`.
- A nested record primary constructor takes `ActivityBadge`, but `ActivityBadge` is declared inside that nested record body, outside primary-constructor scope.
- A field, property, event, nested type, or primary-constructor generated property reuses a member name in the same type in a way that is not ordinary method overloads.
- A `var` local uses a conditional expression with a value-type-looking branch and a plain `null` branch, such as `var meters = trackKm is { } km ? km * 1000d : null;`. Use an explicit nullable local type or cast the null branch, such as `: (double?)null`.
- A known third-party enum reference uses a member not present in the local validation contract, such as `TelemCommandModeTypes.ToStart` instead of `TelemCommandModeTypes.Start` for `irsdkSharp` 0.9.0.

When editing snapshots/read models, prefer helper names that describe the operation, such as `FormatSkiesLabel` or `DetermineDeclaredWetSurfaceMismatch`, instead of names identical to public generated properties.

Before trusting this scanner after changing it, run:

```bash
python3 skills/tmr-overlay-validation/scripts/check-csharp-member-duplicates.py --self-test
```

## Windows .NET Gate

When `dotnet` is available, use the same production gate as CI:

```powershell
dotnet restore .\tmrOverlay.sln
dotnet build .\tmrOverlay.sln --configuration Release --no-restore
dotnet test .\tmrOverlay.sln --configuration Release --no-build
```

For quick app-focused checks during Windows development, use:

```powershell
dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
dotnet test .\tmrOverlay.sln
```

If `dotnet` is not available locally, state that clearly. The local scanner is a fallback, not a replacement for Windows build/test verification; Windows compile errors can still appear for API surface mismatches the scanner does not model.

## Python Tools

When touching `tools/analysis/*.py`, run `py_compile` over the touched Python tools. For the current repo tools this is usually:

```bash
env PYTHONPYCACHEPREFIX=/tmp/tmr-pycache python3 -m py_compile tools/analysis/export_history_from_capture.py tools/analysis/yaml_forensics.py
```

If additional Python analysis tools exist in the checkout, include them in the same command rather than leaving changed tools uncompiled.

## macOS Harness

When touching `local-mac/TmrOverlayMac/`, run from that directory when the local toolchain supports it:

```bash
swift build
```

```bash
swift test
```

The current local Command Line Tools setup may build but fail to run XCTest; report that limitation if it applies.

For overlay behavior changes, also run a live mac-harness smoke test when a GUI session is available. Use an isolated app-data root so the check does not disturb normal local settings/history:

```bash
TMR_MAC_APP_DATA_ROOT=/tmp/tmroverlay-live-validate ./run.sh
```

Let it run long enough to connect and start mock live analysis, then confirm:

- the process is running as `TmrOverlayMac`
- the log contains mock connection and collection-start entries
- `logs/performance/*.jsonl` shows nonzero telemetry frames and overlay refresh data
- `logs/overlay-diagnostics/*-live-overlay-diagnostics.json` is being written

This live smoke test is the best check that one instance of each current overlay updates from live mock telemetry. Screenshot generation is still useful as the deterministic layout/regression check, but it should not be treated as the only validation for live behavior. If screen capture or Accessibility permissions are unavailable from the shell, report that limitation and rely on process/log/diagnostic evidence plus the screenshot generator.

## Change-Specific Checks

- Keep public snapshot member names stable unless the requested change intentionally updates the contract.
- If changing shared live models or normalized snapshot contracts, keep existing overlay-specific members additive/stable, add focused builder tests, and do not change durable history/raw-capture schemas unless explicitly required.
- If changing raw capture format, verify `docs/capture-format.md`, `telemetry.md`, and `README.md` were updated in the same pass.
- If changing IBT capture or analysis behavior, verify `docs/ibt-analysis.md` when present, `docs/capture-format.md`, `telemetry.md`, and `README.md` stay aligned. IBT analysis must remain a compact sidecar: no source `.ibt` copy by default, bounded candidate scanning, file-size and stability checks, timeout/sample limits, and skipped/failed status files instead of failures in compact history, post-race analysis, or capture synthesis.
- If changing shared contracts, reusable overlay behavior, or app boilerplate, inspect whether both the Windows app and ignored mac harness need matching changes.
