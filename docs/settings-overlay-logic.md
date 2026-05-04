# Settings And Overlay Manager Logic

This file explains how the main settings window and overlay manager decide what appears.

Implementation files:

- `src/TmrOverlay.App/Overlays/OverlayManager.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`
- `src/TmrOverlay.App/Overlays/Abstractions/PersistentOverlayForm.cs`

## Product Shape

The settings window is the main UI. It is a normal desktop window, not a driving overlay.

Treat settings as the app control plane rather than a v1 or model-v2 overlay consumer. Overlay v2 migrations change which live model a driving overlay reads; settings owns lifecycle, visibility, session filters, shared units/fonts, diagnostics access, and future app-platform controls. Settings information architecture should stay separate from telemetry model migration.

Driving overlays are managed windows that can sit above the simulator. Current driving overlays:

- Status.
- Standings.
- Fuel Calculator.
- Relative.
- Track Map.
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

- Uses default size from `SettingsOverlayDefinition`; the current fixed client size is wide enough for the Support tab's two-column controls.
- Enforces its default size as both the minimum and maximum.
- Recenters every time it is opened.
- Does not preserve user-dragged placement between runs.
- Uses normal desktop/taskbar behavior and is not always on top.
- Activates after being shown.

This differs from driving overlays, which persist placement through `PersistentOverlayForm`.

## Settings Tabs

The settings UI has:

- A branded `Tech Mates Racing Overlay` header with the TMR logo.
- Flat vertical left-side tabs in a taller desktop settings window so all current overlay and support tabs fit without scrolling.
- General.
- User-facing overlay tabs ordered by common race workflow.
- Support as the final tab.

Managed overlay tabs are generated from `OverlayManager.ManagedOverlayDefinitions`, then filtered so internal-only surfaces such as Collector Status do not appear as user-facing settings tabs. Adding a driving overlay definition there is what makes the settings tab eligible to appear; overlay-specific options only appear when the definition provides option descriptors or a custom settings block.

The tab list keeps General first, puts common race timing overlays early, keeps driver telemetry and race-state overlays after that, and leaves Support last. Future platform surfaces such as Overlay Bridge, publishing/update status, post-race analysis, and overlay builder should not be added as ordinary driving-overlay tabs without a product pass.

Flags exposes flag-category enable controls and custom border size controls instead of session filters, scale, opacity, or custom timing. The flag overlay ignores background flag bits such as serviceable/start-hidden as standalone display triggers and paints a category only while current telemetry reports a matching user-facing flag category.

Most driving overlays expose opacity next to scale and keep the existing overlay opacity as their default. The transparent full-screen-style flag border and the radar do not expose opacity because their readability comes from semantic color/shape rather than a normal panel background.

Gap To Leader is race-only, so its settings tab omits the redundant `Display in sessions` filter group.

Each user-facing overlay tab shows a selectable localhost browser-source URL plus a copy button when a route exists. These routes are independent of the native overlay visibility checkbox; a hidden native overlay can still be used as a localhost browser source when `LocalhostOverlays` is enabled in configuration.

Track Map exposes normal overlay visibility, scale, opacity, and session filters plus map-source status. Its tab explains that bundled app maps are used automatically when the current track identity matches, while local IBT-derived map generation is on by default to fill or improve layouts after sessions. The `Build local maps from IBT telemetry` checkbox controls future automatic post-session generation and whether user-generated maps are eligible at runtime; when disabled, runtime lookup uses bundled app maps before falling back to the circle placeholder. The settings tab no longer exposes one-off `.ibt` conversion controls; use `tools/TmrOverlay.TrackMapGenerator` for bundled-asset generation and QA.

Stream Chat is exposed as a browser-source settings tab and localhost route. The settings tab asks for one active source: not configured, Streamlabs Chat Box URL, or Twitch channel. Streamlabs embeds the configured widget URL in `/overlays/stream-chat`; Twitch connects from the browser source to public channel chat and appends messages as they arrive. The inactive provider is ignored even if its field has a saved value. Provider auth, moderation controls, write/chat-command support, and richer rate-limit handling are separate follow-up work. Streamlabs widget URLs are treated as private local settings and redacted from diagnostics bundles.

The selected overlay tab is reported back to `OverlayManager`.

The General tab exposes the metric/imperial unit selector. User-facing font selection is intentionally hidden for now so Windows and mac review surfaces stay stable; shared typography remains a theme/platform default that can be revisited from the visual-token layer. Support capture and support paths intentionally live in the Support tab instead of General.

## Overlay Settings Normalization

For every managed driving overlay, `OverlayManager` ensures:

- An `OverlaySettings` entry exists.
- Scale is clamped from `0.6` to `2.0`.
- Width and height are valid.
- Invalid sizes are reset from the overlay default size and scale.

The gap-to-leader overlay is normalized to race-only visibility whenever managed overlay settings are ensured.

The flags overlay is normalized to the primary monitor bounds when old saved settings still contain the previous small table dimensions or the old 1920x1080 default. Ultrawide monitors use full monitor height with a centered 4:3 border so the default is not an impractically wide banner. User-entered custom border width/height are preserved once edited.

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
3. If the radar overlay is enabled, the radar overlay is forced visible even if filtered out by session rules.
4. `CarRadarForm.SetSettingsPreviewVisible(true)` forces radar alpha to full so the user can see changes.

When leaving the radar tab, preview mode is turned off. If the radar overlay is disabled, selecting its settings tab does not override the `Visible` checkbox.

## Scale And Units

Scale:

- Each overlay scale is clamped from `0.6` to `2.0`.
- Client size is recalculated from default dimensions and scale.
- The manager remembers the last applied scale to avoid unnecessary resizing.

Units:

- If the global unit system changes, all managed driving overlay forms are closed and recreated.
- Unit system is either `Metric` or `Imperial`.

Typography:

- The user-facing font selector is hidden while shared typography stays a theme/platform concern.
- Managed overlays render with the platform/theme default font unless an advanced theme override is introduced.

## Settings Persistence

Driving overlays persist their placement and size through their `OverlaySettings`.

The settings window stores normalized size but is recentered on open, so saved coordinates do not control startup placement.

## Diagnostics In Settings

The Support tab:

- Shows compact app status, live/session state, and current app warning/error.
- Makes support actions primary: create diagnostics bundle, copy latest bundle path, and open the diagnostics folder.
- Owns runtime diagnostic telemetry capture requests without overlapping the current-issue state.
- Keeps local storage shortcuts together for logs, diagnostics, captures, and history.
- Shows advanced collection systems as discoverable disabled-by-default support tools instead of normal product tabs.
- Shows compact app activity for telemetry, iRacing connection health, raw capture state, and process size.
- Reports the latest automatic end-of-session diagnostics bundle.

## Design Notes

- Keep settings as a normal desktop UI.
- Keep driving overlays focused and simulator-friendly.
- If an overlay has preview behavior, document which settings tab triggers it.
- If new overlay visibility rules are added, update this file and the overlay-specific logic doc.
