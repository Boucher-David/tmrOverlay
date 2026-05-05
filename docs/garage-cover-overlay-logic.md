# Garage Cover Overlay Logic

`Garage Cover` is a streamer privacy overlay. It is disabled by default and only appears after the user enables it in settings.

## Visibility

The overlay reads `LiveTelemetrySnapshot.Models.RaceEvents.IsGarageVisible`, which is derived from iRacing's `IsGarageVisible` telemetry value. This is intentionally different from `IsInGarage`: the cover should react to the visible Garage/setup screen, not just whether the car physics are in a garage state.

The Windows overlay is managed by `OverlayManager`, but the form self-hides until all of these are true:

- the overlay is enabled in settings
- live telemetry is connected and collecting
- the latest live snapshot is fresh
- `IsGarageVisible` is true

When any condition clears, the cover hides.

## Display

The first-run default frame is a centered 16:9 cover region, capped at half the primary screen and at the overlay's 1280x720 design size. Earlier tester builds defaulted to the primary screen bounds; saved screen-sized defaults are migrated down to this compact frame because a full-screen privacy cover can block emergency access to the settings UI. The user can still adjust width and height in the Garage Cover settings tab and can drag the native overlay like other product overlays.

iRacing currently gives the app `IsGarageVisible` / `IsInGarage` state through telemetry, not a reliable Garage panel rectangle. Until there is validated geometry for the actual on-screen Garage UI, the safer default is to cover less of the desktop and let the streamer enlarge the frame deliberately.

The cover is always opaque. It does not expose the normal opacity control because semi-transparent setup coverage can leak private setup information.

When the settings window is active, `OverlayManager` drops Garage Cover out of the topmost layer and brings settings forward. This keeps Alt+Tab to the settings UI available as the emergency shutdown path even if Garage Cover is visible.

The user can import a PNG, JPG, BMP, or GIF image. The app copies that file into app-owned settings storage under `garage-cover/cover.*` and stores that app-owned path in overlay settings. Runtime rendering loads from the app-owned copy, not the original selected file.

If no image is imported, or the saved image cannot be loaded, the overlay paints a black fallback with the TMR logo centered on the cover. If the logo asset cannot be loaded, it falls back one more time to centered `TMR` text. The fallback still covers setup details.

## Product Boundary

V1 is deliberately a privacy cover, not a rich broadcast panel. It should avoid showing race information that could make the setup-cover state harder to trust.

V2 can treat the Garage-visible state as a broadcast context trigger. Once model-v2 race analysis and layout primitives are stronger, the same surface could optionally show safe stream-facing information such as session timing, standings excerpts, sponsor/team art, recent stint summaries, or upcoming pit/strategy context while continuing to hide setup details.
