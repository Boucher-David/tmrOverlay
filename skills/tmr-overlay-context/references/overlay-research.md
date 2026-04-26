# Overlay Research

Last updated: 2026-04-26

## External Reference Reviewed

Reviewed:

- `https://ioverlay.app/`

Purpose of the review:

- understand common overlay types used by iRacing drivers
- understand visual density and placement patterns
- inform the first useful overlays without copying implementation details

## Overlay Types Noted

From the public site and help pages, the reference product appears to support multiple overlay families such as:

- standings
- relative
- fuel
- track map
- flat map
- spotter / incident indicator
- pit entry / pit exit helper
- multiclass traffic indicator
- Twitch chat
- garage/setup cover
- race control

That confirms there is a mature market expectation for multiple small purpose-built overlays rather than one monolithic dashboard.

## Visual Patterns Worth Keeping In Mind

Observed design tendencies:

- compact widgets intended for corners and screen edges
- dark translucent panels rather than bright chrome
- dense information layout with strong semantic color
- tables for standings/relative
- compact matrix-style layouts for fuel and strategy information
- multiple overlays visible at once rather than one full-screen surface

## Fuel Overlay Takeaways

The most relevant external reference is the fuel overlay direction.

The useful pattern is not just:

- current fuel

It is usually a combination of:

- current fuel
- recent / rolling consumption
- projected stint length
- projected laps remaining
- refuel target or fuel-to-finish style guidance
- pit-window or strategy cues

That aligns with the current direction for `tmrOverlay`.

## Practical Constraints Mentioned In The Research

Public help/FAQ notes from the reference product suggested:

- overlays are intended for borderless or windowed mode
- incomplete max-cars settings can reduce telemetry completeness for position/fuel-related features

That is useful context for future troubleshooting if a user reports missing live data in larger sessions.

## Design Direction For This Repo

Use the research as a benchmark for usefulness, not as a design template.

Good direction for `tmrOverlay`:

- small, purpose-driven overlays
- low-noise dark styling
- clear semantic state color
- prioritize live decision support over decorative telemetry
- build fuel/stint first, then expand to session-relative and strategy overlays

