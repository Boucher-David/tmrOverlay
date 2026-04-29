# Settings Overlay Mock

Generated preview screenshots for the main settings window.

- `settings-overlay-states.png` is the current mac-harness contact sheet.
- `states/general.png` shows shared font/units plus copy-only Windows build commands.
- `states/error-logging.png` shows support paths, current issue, diagnostics bundle, and performance snapshot.
- `states/overlay-tab.png` shows per-overlay visibility, scale, session filters, and overlay-specific options.
- `states/post-race-analysis.png` shows saved strategy analysis browsing.

The settings window is a normal desktop window, not an always-on-top driving overlay. It uses a centered 1080x600 panel with a `TMR Overlay` header, vertical left-side tabs, a General tab, Error Logging tab, one tab per current overlay, a placeholder Overlay Bridge tab, and a post-race analysis tab. The radar tab forces a live radar preview while it is selected.

The overlay tabs expose:

- visibility
- overlay scale
- session filters for test, practice, qualifying, and race
- overlay-specific display options

The General tab exposes a font-family selector using widely available fonts plus a metric/imperial unit selector. The Collector Status tab owns the runtime raw-capture toggle.

The Error Logging tab reports the latest diagnostics bundle. New bundles are generated automatically when a live telemetry session finalizes, while the manual button remains available for on-demand support capture.

The Overlay Bridge tab is intentionally placeholder-only for now. It reserves UI space for post-v1.0 bridge controls while the disabled-by-default Windows bridge continues to be configured through app settings.

Generated PNGs are the source of truth for runtime screenshot review.
