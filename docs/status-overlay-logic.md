# Status Overlay Logic

This file explains how the status overlay decides what to show.

Implementation file:

- `src/TmrOverlay.App/Overlays/Status/StatusOverlayForm.cs`

## Purpose

The status overlay is a compact health panel for the app, telemetry connection, live analysis, optional raw capture, and local diagnostics. It does not make race-strategy decisions. It translates `TelemetryCaptureState` into a small set of user-visible health states.

## Refresh Loop

The overlay refreshes every 250 ms.

Each refresh:

1. Reads a `TelemetryCaptureStatusSnapshot` from `TelemetryCaptureState`.
2. Converts that snapshot into a `CaptureHealth` object.
3. Applies colors and labels from that health object.
4. Shows or hides detail rows based on overlay settings.
5. Records performance metrics for snapshot read, health derivation, UI apply, paint, and total refresh.

## Visible Rows

The overlay contains:

- Indicator dot.
- Title: `TmrOverlay`.
- Status row.
- Detail row.
- Capture row.
- Health/message row.

The capture row is controlled by `StatusCaptureDetails`.

The health/message row is controlled by `StatusHealthDetails`.

## Health Inputs

The health calculation uses:

- Whether the app is connected to iRacing.
- Whether telemetry collection has started.
- Whether raw capture is enabled.
- Current or last capture directory.
- Captured frame count.
- Written frame count.
- Dropped frame count.
- Last captured frame timestamp.
- Last disk-write timestamp.
- Telemetry file size.
- Last app error.
- Last app warning.
- Last capture warning.

## Health State Priority

The first matching rule wins.

1. If `LastError` exists:
   - Level: error.
   - Status: `Capture error`.
   - Message: trimmed error text.

2. If not connected:
   - Level: warning.
   - Status: `Waiting for iRacing`.
   - Detail: `collector idle`.
   - Message says the sim is not connected.

3. If connected but not collecting:
   - Level: warning.
   - Status: `Connected, waiting for telemetry`.
   - Detail says it is waiting for the first telemetry frame.

4. If raw capture is enabled, frames have arrived, and no frames have been written:
   - Level: error.
   - Status: `Frames queued, not written`.
   - Message says disk writer has not confirmed writes.

5. If raw capture is enabled and written frames are more than two ahead of queued frames:
   - Level: warning.
   - Status: `Capture counters inconsistent`.

6. If dropped frames are greater than zero:
   - Level: warning.
   - Status: `Collecting with dropped frames`.
   - Message says the capture queue overflowed.

7. If the last SDK frame is more than 5 seconds old:
   - Level: error.
   - Status: `Telemetry frames stalled`.

8. If raw capture is enabled and the last disk write is more than 5 seconds old:
   - Level: error.
   - Status: `Disk writes stalled`.

9. If `LastWarning` exists:
   - Level: warning.
   - Status: `Collecting with warning`.

10. If app warning exists, such as stale build detection:
    - Level: warning.
    - Status: `Build may be stale`.

11. Otherwise:
    - Level: ok.
    - Status is `Collecting raw telemetry` when raw capture is enabled.
    - Status is `Analyzing live telemetry` when raw capture is disabled.

## Colors

The status overlay applies state colors from `OverlayTheme`:

- Error: error background and indicator.
- Warning: warning background and indicator.
- Capturing with ok health: success background and indicator.
- Connected but not collecting: warning background and indicator.
- Not connected: neutral background and indicator.

## Raw Capture Text

When raw capture is enabled:

- Capture text shows a compact path to the current or last capture directory.
- Detail text shows queued frames, written frames, drops, and file size.

When raw capture is disabled:

- Capture text says `raw: disabled; history ready`.
- Detail text shows live frame count and that history is on.

## Design Notes

- This overlay should stay diagnostic, not strategic.
- It should warn for stale live frames even when raw capture is disabled.
- Raw-capture disk warnings should only appear when raw capture is enabled.
- Empty states should not show local machine history or cached session details as live data.

