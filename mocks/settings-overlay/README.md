# Settings Overlay Mock

Generated preview screenshots for the main settings window.

- `settings-overlay-states.png` is the current mac-harness contact sheet.
- `states/general.png` shows shared unit settings.
- `states/support.png` shows diagnostic capture, support paths, current issue, diagnostics bundle, and performance snapshot.
- `states/standings-overlay.png` and `states/overlay-tab.png` show the V2 Standings and Relative content-row session matrices.
- `states/race-only-overlay.png` shows the race-only Gap To Leader tab without redundant session filters.
- `states/fuel-calculator-overlay.png`, `states/input-state-overlay.png`, `states/car-radar-overlay.png`, `states/flags-overlay.png`, `states/track-map-overlay.png`, `states/stream-chat-overlay.png`, and `states/garage-cover-overlay.png` show the converted V2 settings surfaces for those overlays.

The settings window is a normal desktop window, not an always-on-top driving overlay. It uses a centered fixed-size 1080x600 panel with a TMR logo plus `Tech Mates Racing Overlay` header, vertical left-side tabs, General first, user-facing overlays next, and Support last. The V2 settings surfaces reuse the same left-tab order and current production settings contracts while replacing the visual shell. Overlay tabs expose opacity where it applies; flags and radar omit it. The radar tab previews the radar overlay only when that overlay is enabled.

The overlay tabs expose:

- visibility
- overlay scale
- session filters for test, practice, qualifying, and race
- overlay-specific display options

The General tab exposes a metric/imperial unit selector. User-facing font selection is hidden during the parity pass so mac and Windows screenshots stay stable; font choice remains an advanced theme/platform concern. The Support tab owns runtime diagnostic capture and reports the latest diagnostics bundle. New bundles are generated automatically when a live telemetry session finalizes, while the manual button remains available for on-demand support capture.

Generated PNGs are the source of truth for runtime screenshot review.
