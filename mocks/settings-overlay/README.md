# Settings Overlay Mock

Generated preview screenshots for the main settings window.

- `settings-overlay-states.png` is the current mac-harness contact sheet.
- `states/general.png` shows shared font and unit settings.
- `states/support.png` shows diagnostic capture, support paths, current issue, diagnostics bundle, and performance snapshot.
- `states/overlay-tab.png` shows per-overlay visibility, scale, session filters, and overlay-specific options.
- `states/race-only-overlay.png` shows the race-only Gap To Leader tab without redundant session filters.

The settings window is a normal desktop window, not an always-on-top driving overlay. It uses a centered fixed-size 1080x600 panel with a `TMR Overlay` header, vertical left-side tabs, General first, user-facing overlays next, and Support last. Overlay tabs expose opacity where it applies; flags and radar omit it. The radar tab previews the radar overlay only when that overlay is enabled.

The overlay tabs expose:

- visibility
- overlay scale
- session filters for test, practice, qualifying, and race
- overlay-specific display options

The General tab exposes a font-family selector using widely available fonts plus a metric/imperial unit selector. The Support tab owns runtime diagnostic capture and reports the latest diagnostics bundle. New bundles are generated automatically when a live telemetry session finalizes, while the manual button remains available for on-demand support capture.

Generated PNGs are the source of truth for runtime screenshot review.
