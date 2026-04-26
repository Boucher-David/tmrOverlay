# Overlay Modules

Each overlay type should live in its own folder under `Overlays/`.

Overlay modules own presentation-specific code:

- default size and identity
- view/form implementation
- overlay-specific labels, colors, and layout
- overlay-specific persisted settings

Overlay modules should not talk directly to iRacing or raw capture files. They should consume shared state, metrics, and history services from the app/core layers so multiple overlays can reuse the same telemetry interpretation.

Current modules:

- `Status/` - tiny collector status overlay

Expected future modules:

- `Fuel/`
- `Relative/`
- `Standings/`
- `PitHelper/`
- `RaceControl/`
