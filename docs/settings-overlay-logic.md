# Settings And Overlay Manager Logic

This file explains how the main settings window and overlay manager decide what appears.

Implementation files:

- `src/TmrOverlay.App/Overlays/OverlayManager.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/Abstractions/PersistentOverlayForm.cs`

## Product Shape

The settings window is the main UI. It is a normal desktop window, not a driving overlay.

Treat settings as the app control plane rather than a v1 or model-v2 overlay consumer. Overlay v2 migrations change which live model a driving overlay reads; settings owns lifecycle, visibility, session filters, shared units/fonts, diagnostics access, and future app-platform controls. A later settings information-architecture pass may group overlays and platform features, but it should stay separate from telemetry model migration.

Driving overlays are managed windows that can sit above the simulator. Current driving overlays:

- Status.
- Fuel Calculator.
- Relative.
- Flags.
- Session / Weather.
- Pit Service.
- Input / Car State.
- Car Radar.
- Gap To Leader.

## Startup

At startup, `OverlayManager.ShowStartupOverlays`:

1. Loads settings.
2. Ensures managed overlay settings exist.
3. Opens the settings window.
4. Applies overlay settings to driving overlays.
5. Saves normalized settings.
6. Starts a 1 second timer that reapplies session visibility rules.

## Settings Window Placement

The settings window:

- Uses default size from `SettingsOverlayDefinition`.
- Enforces its minimum default size.
- Recenters every time it is opened.
- Does not preserve user-dragged placement between runs.
- Activates after being shown.

This differs from driving overlays, which persist placement through `PersistentOverlayForm`.

## Settings Tabs

The settings UI has:

- A `TMR Overlay` header.
- Vertical left-side tabs.
- General.
- Error Logging.
- One tab per managed overlay.
- Overlay Bridge placeholder.
- Post-race Analysis.

Managed overlay tabs are generated from `OverlayManager.ManagedOverlayDefinitions`. Adding a driving overlay definition there is what makes the settings tab appear; overlay-specific options only appear when the definition provides option descriptors.

The selected overlay tab is reported back to `OverlayManager`.

The General tab exposes copyable Windows PowerShell commands for local development:

- Clean clears Release build intermediates, app/core `bin` and `obj` folders, the custom `artifacts/TmrOverlay-win-x64` publish folder, and its zip.
- Build compiles the WinForms app in Release.
- Publish writes the self-contained `win-x64` tester build to `artifacts/TmrOverlay-win-x64`.
- Zip packages the current publish folder and throws a clear "Run Publish first" error when the publish output folder is missing.

The settings UI only copies these commands. It never runs builds from inside the app.

## Overlay Settings Normalization

For every managed driving overlay, `OverlayManager` ensures:

- An `OverlaySettings` entry exists.
- Scale is clamped from `0.6` to `2.0`.
- Width and height are valid.
- Invalid sizes are reset from the overlay default size and scale.

The gap-to-leader overlay receives a one-time default of race-only visibility if it still has all session filters enabled and the marker option has not been set.

## Session Visibility

Every second, and whenever settings change, `OverlayManager`:

1. Reads the latest live telemetry context.
2. Classifies the session string into Test, Practice, Qualifying, Race, or unknown.
3. For each driving overlay, checks:
   - Overlay enabled.
   - Allowed for current session kind.
   - Special radar settings preview state.
4. Shows or hides each overlay.

Unknown session kind is allowed by default.

Session classification is string based:

- Contains `test`: Test.
- Contains `practice`: Practice.
- Contains `qual`: Qualifying.
- Contains `race`: Race.

## Radar Settings Preview

When the user selects the radar settings tab:

1. `SettingsOverlayForm` reports the selected tab id.
2. `OverlayManager` sees that the selected overlay id is `car-radar`.
3. The radar overlay is forced visible even if disabled or filtered out by session rules.
4. `CarRadarForm.SetSettingsPreviewVisible(true)` forces radar alpha to full so the user can see changes.

When leaving the radar tab, preview mode is turned off.

## Scale, Font, And Units

Scale:

- Each overlay scale is clamped from `0.6` to `2.0`.
- Client size is recalculated from default dimensions and scale.
- The manager remembers the last applied scale to avoid unnecessary resizing.

Font:

- If the global font family changes, all managed driving overlay forms are closed and recreated.

Units:

- If the global unit system changes, all managed driving overlay forms are closed and recreated.
- Unit system is either `Metric` or `Imperial`.

## Settings Persistence

Driving overlays persist their placement and size through their `OverlaySettings`.

The settings window stores normalized size but is recentered on open, so saved coordinates do not control startup placement.

## Post-Race Analysis Tab

The settings window:

1. Loads recent post-race analyses from `PostRaceAnalysisStore`.
2. Sorts saved analyses newest first through the store.
3. Selects the first item by default.
4. Displays the selected analysis body as plain text.

The built-in sample appears after saved analyses.

## Diagnostics In Settings

The Error Logging tab:

- Shows current app warning/error.
- Shows performance summary text, including live iRacing channel/system values and overlay update-decision rates.
- Opens local logs and diagnostics folders.
- Can manually create/copy a diagnostics bundle.
- Reports the latest automatic end-of-session diagnostics bundle.

## Design Notes

- Keep settings as a normal desktop UI.
- Keep driving overlays focused and simulator-friendly.
- If an overlay has preview behavior, document which settings tab triggers it.
- If new overlay visibility rules are added, update this file and the overlay-specific logic doc.
