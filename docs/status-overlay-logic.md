# Status / Diagnostics Logic

This file explains how the app status diagnostics decide what to show.

Implementation file:

- `src/TmrOverlay.App/Diagnostics/AppDiagnosticsStatusModel.cs`
- `src/TmrOverlay.App/Overlays/SettingsPanel/SettingsOverlayForm.cs`

## Purpose

The status diagnostics model is the shared health surface for the app, telemetry connection, live analysis, optional raw capture, and local diagnostics. It does not make race-strategy decisions. It translates `TelemetryCaptureState` into a small set of user-visible health states for the Support tab and diagnostics bundle metadata.

The former floating Collector Status overlay is not a normal V1 user overlay. The supported product surface is the Settings Support tab, while the underlying status model remains available to diagnostics and future support-only tooling.

## Refresh Loop

The Settings Support tab periodically:

1. Reads a `TelemetryCaptureStatusSnapshot` from `TelemetryCaptureState`.
2. Converts that snapshot into an `AppDiagnosticsStatusModel`.
3. Applies app status, session state, current issue, support status, and storage/diagnostic labels from that health object.
4. Records performance and diagnostics state through the shared app-performance and diagnostics bundle paths.

## Visible Fields

The Support tab contains:

- App status.
- Session state.
- Current issue.
- Support bundle status.
- Diagnostic capture guidance.
- Storage shortcuts.

## Health Inputs

The shared diagnostics status model is used by the Support tab so app status, session state, current issue text, and diagnostics bundle summaries do not drift.

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
   - Level: neutral, unless an app warning exists.
   - Status: `Waiting for iRacing`.
   - Detail: `collector idle`.
   - Message says the sim is not connected.

3. If connected but not collecting:
   - Level: info, unless an app warning exists.
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

The Support tab applies state colors from `OverlayTheme`:

- Error: error text/background.
- Warning: warning text/background.
- Healthy live collection: success text/background.
- Connected but not collecting: info text/background.
- Not connected: neutral text/background.

## Raw Capture Text

When raw capture is enabled:

- Capture text shows a compact path to the current or last capture directory.
- Detail text shows queued frames, written frames, drops, and file size.

When raw capture is disabled:

- Capture text says `raw: disabled; history ready`.
- Detail text shows live frame count and that history is on.

## Design Notes

- This surface should stay diagnostic, not strategic.
- It should warn for stale live frames even when raw capture is disabled.
- Raw-capture disk warnings should only appear when raw capture is enabled.
- Empty states should not show local machine history or cached session details as live data.
