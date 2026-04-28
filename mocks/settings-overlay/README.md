# Settings Overlay Mock

This folder captures the first-pass settings overlay direction: a centered 600x600 tabbed panel with a General tab, one tab per current overlay, a placeholder Overlay Bridge tab, and a post-race analysis tab.

The overlay tabs expose:

- visibility
- overlay scale
- session filters for test, practice, qualifying, and race
- overlay-specific display options

The General tab exposes a font-family selector using widely available fonts plus a metric/imperial unit selector. The Collector Status tab owns the runtime raw-capture toggle.

The Overlay Bridge tab is intentionally placeholder-only for now. It reserves UI space for post-v1.0 bridge controls while the disabled-by-default Windows bridge continues to be configured through app settings.
