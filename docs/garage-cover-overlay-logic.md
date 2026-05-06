# Garage Cover Overlay Logic

`Garage Cover` is a localhost browser-source streamer privacy surface. It is not created as a native desktop overlay.

## Visibility

The overlay reads `LiveTelemetrySnapshot.Models.RaceEvents.IsGarageVisible`, which is derived from iRacing's `IsGarageVisible` telemetry value. This is intentionally different from `IsInGarage`: the cover should react to the visible Garage/setup screen, not just whether the car physics are in a garage state.

The browser-source route is served at `/overlays/garage-cover`. It keeps the page transparent until all of these are true:

- live telemetry is connected and collecting
- the latest live snapshot is fresh
- `IsGarageVisible` is true

When any condition clears, the browser source fades out.

## Display

The app does not manage desktop placement or size for Garage Cover. The user sizes and crops the browser source in OBS or another local capture tool.

The cover is always opaque. It does not expose the normal opacity control because semi-transparent setup coverage can leak private setup information.

The user can import a PNG, JPG, BMP, or GIF image. The app copies that file into app-owned settings storage under `garage-cover/cover.*` and stores that app-owned path in overlay settings. Runtime rendering loads from the app-owned copy, not the original selected file.

The localhost server exposes `GET /api/garage-cover` for image metadata and `GET /api/garage-cover/image` for the app-owned imported image. If no image is imported, or the saved image cannot be loaded, the browser source paints a black fallback with centered `TMR` text. The fallback still covers setup details.

## Product Boundary

V1 is deliberately a privacy cover, not a rich broadcast panel. It should avoid showing race information that could make the setup-cover state harder to trust.

V2 can treat the Garage-visible state as a broadcast context trigger. Once model-v2 race analysis and layout primitives are stronger, the same surface could optionally show safe stream-facing information such as session timing, standings excerpts, sponsor/team art, recent stint summaries, or upcoming pit/strategy context while continuing to hide setup details.
