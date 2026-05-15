#!/usr/bin/env python3
"""Validate generated overlay screenshot artifacts without external image packages."""

from __future__ import annotations

import argparse
import json
import re
import struct
import sys
import zlib
from pathlib import Path
from typing import Optional


EXPECTED_PNGS = {
    "design-v2/design-v2-states.png": (5350, 4020),
    "design-v2/design-v2-components-outrun.png": (5350, 5240),
    "fuel-calculator/fuel-calculator-states.png": (3600, 2800),
    "relative/relative-states.png": (3600, 2800),
    "track-map/track-map-sector-states.png": (5350, 2800),
    "settings-overlay/settings-overlay-states.png": (5350, 6460),
    "settings-overlay/settings-components.png": (5350, 4020),
    "car-radar/car-radar-states.png": (3600, 2800),
    "car-radar/car-radar-multiclass.png": (600, 600),
    "gap-to-leader/gap-to-leader-states.png": (3600, 2800),
    "gap-to-leader/gap-to-leader.png": (1120, 520),
}

EXPECTED_STATE_PNGS = [
    "design-v2/states/standings-telemetry.png",
    "design-v2/states/relative-telemetry.png",
    "design-v2/states/sector-comparison.png",
    "design-v2/states/blindspot-signal.png",
    "design-v2/states/laptime-delta.png",
    "design-v2/states/stint-laptime-log.png",
    "design-v2/states/flag-display.png",
    "design-v2/states/analysis-exception.png",
    "design-v2/components/outrun/sidebar-tab.png",
    "design-v2/components/outrun/buttons.png",
    "design-v2/components/outrun/controls.png",
    "design-v2/components/outrun/status-pills.png",
    "design-v2/components/outrun/section-panel.png",
    "design-v2/components/outrun/table-rows.png",
    "design-v2/components/outrun/graph-chrome.png",
    "design-v2/components/outrun/overlay-shell.png",
    "design-v2/components/outrun/localhost-block.png",
    "design-v2/components/outrun/settings-content-block.png",
    "fuel-calculator/states/waiting.png",
    "fuel-calculator/states/opening-stint.png",
    "fuel-calculator/states/mid-race.png",
    "fuel-calculator/states/stable-finish.png",
    "relative/states/waiting.png",
    "relative/states/live-relative.png",
    "relative/states/compact-window.png",
    "relative/states/pit-context.png",
    "track-map/states/normal.png",
    "track-map/states/sector-personal-best.png",
    "track-map/states/session-best-lap.png",
    "track-map/states/following-sector-one.png",
    "track-map/states/mixed-live-sectors.png",
    "settings-overlay/states/general.png",
    "settings-overlay/states/support.png",
    "settings-overlay/states/overlay-tab.png",
    "settings-overlay/states/race-only-overlay.png",
    "settings-overlay/states/fuel-calculator-overlay.png",
    "settings-overlay/states/session-weather-overlay.png",
    "settings-overlay/states/pit-service-overlay.png",
    "settings-overlay/states/track-map-overlay.png",
    "settings-overlay/states/stream-chat-overlay.png",
    "settings-overlay/states/input-state-overlay.png",
    "settings-overlay/states/car-radar-overlay.png",
    "settings-overlay/states/flags-overlay.png",
    "settings-overlay/states/garage-cover-overlay.png",
    "car-radar/states/clear-track.png",
    "car-radar/states/side-pressure.png",
    "car-radar/states/multiclass-approaching.png",
    "car-radar/states/error-reporting.png",
    "gap-to-leader/states/waiting-for-timing.png",
    "gap-to-leader/states/tight-early-field.png",
    "gap-to-leader/states/pit-weather-handoff.png",
    "gap-to-leader/states/long-run-spread.png",
]

EXPECTED_COMPONENT_PNGS = {
    "settings-overlay/components/sidebar-tabs.png": (380, 1012),
    "settings-overlay/components/region-tabs.png": (840, 104),
    "settings-overlay/components/unit-choice.png": (784, 264),
    "settings-overlay/components/overlay-controls.png": (784, 452),
    "settings-overlay/components/content-matrix.png": (1380, 444),
    "settings-overlay/components/chat-inputs.png": (1300, 408),
    "settings-overlay/components/support-buttons.png": (1300, 348),
    "settings-overlay/components/browser-source.png": (1300, 140),
}

WINDOWS_EXPECTED_PNGS = {
    "states/fuel-calculator-live.png": (600, 340),
    "states/relative-live.png": (520, 360),
    "states/standings-live.png": (780, 520),
    "states/track-map-placeholder.png": (360, 360),
    "states/flags-blue.png": (360, 170),
    "states/session-weather-live.png": (480, 520),
    "states/pit-service-active.png": (420, 560),
    "states/input-state-trace.png": (520, 260),
    "states/car-radar-side-pressure.png": (300, 300),
    "states/gap-to-leader-trend.png": (720, 360),
}

WINDOWS_EXPECTED_SIZE_SOURCES = {
    "states/fuel-calculator-live.png": "src/TmrOverlay.App/Overlays/FuelCalculator/FuelCalculatorOverlayDefinition.cs",
    "states/relative-live.png": "src/TmrOverlay.App/Overlays/Relative/RelativeOverlayDefinition.cs",
    "states/standings-live.png": "src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs",
    "states/track-map-placeholder.png": "src/TmrOverlay.App/Overlays/TrackMap/TrackMapOverlayDefinition.cs",
    "states/session-weather-live.png": "src/TmrOverlay.App/Overlays/SessionWeather/SessionWeatherOverlayDefinition.cs",
    "states/pit-service-active.png": "src/TmrOverlay.App/Overlays/PitService/PitServiceOverlayDefinition.cs",
    "states/input-state-trace.png": "src/TmrOverlay.App/Overlays/InputState/InputStateOverlayDefinition.cs",
    "states/car-radar-side-pressure.png": "src/TmrOverlay.App/Overlays/CarRadar/CarRadarOverlayDefinition.cs",
    "states/gap-to-leader-trend.png": "src/TmrOverlay.App/Overlays/GapToLeader/GapToLeaderOverlayDefinition.cs",
    "states/flags-blue.png": "src/TmrOverlay.App/Overlays/Flags/FlagsOverlayDefinition.cs",
}

WINDOWS_GENERATOR_SIZE_SOURCES = {
}

WINDOWS_MINIMUM_PNGS = {
    # GitHub-hosted Windows runners can clamp wide top-level WinForms clients
    # to the available desktop width even when the app's production client
    # target is wider than the previous 1080x600 shell. Keep the height exact enough for layout review while
    # accepting the runner-observed width.
    "states/settings-general.png": (1000, 680),
    "states/settings-standings.png": (1000, 680),
    "states/settings-relative.png": (1000, 680),
    "states/settings-gap-to-leader.png": (1000, 680),
    "states/settings-track-map.png": (1000, 680),
    "states/settings-stream-chat.png": (1000, 680),
    "states/settings-garage-cover.png": (1000, 680),
    "states/settings-fuel-calculator.png": (1000, 680),
    "states/settings-inputs.png": (1000, 680),
    "states/settings-car-radar.png": (1000, 680),
    "states/settings-flags.png": (1000, 680),
    "states/settings-session-weather.png": (1000, 680),
    "states/settings-pit-service.png": (1000, 680),
    "states/settings-support.png": (1000, 680),
    "components/settings/sidebar-tabs.png": (190, 506),
    "components/settings/region-tabs.png": (420, 52),
    "components/settings/unit-choice.png": (392, 132),
    "components/settings/overlay-controls.png": (392, 226),
    "components/settings/content-matrix.png": (690, 222),
    "components/settings/chat-inputs.png": (650, 204),
    "components/settings/support-buttons.png": (716, 202),
    "components/settings/browser-source.png": (296, 132),
}

WINDOWS_MIN_UNIQUE_BYTES = {
    # Flags is a compact transparent renderer. The generator paints a review
    # backdrop behind its transparent window color, but it should not need the
    # same texture complexity as table/graph views.
    "states/flags-blue.png": 8,
}

WINDOWS_EXPECTED_FILES = [
    "contact-sheet.png",
    "manifest.json",
]

WINDOWS_SETTING_REGION_PNGS = [
    "states/settings-general-preview-practice.png",
    "states/settings-general-preview-qualifying.png",
    "states/settings-general-preview-race.png",
    "states/settings-standings-content.png",
    "states/settings-standings-header.png",
    "states/settings-standings-footer.png",
    "states/settings-relative-content.png",
    "states/settings-relative-header.png",
    "states/settings-relative-footer.png",
    "states/settings-gap-to-leader-content.png",
    "states/settings-gap-to-leader-header.png",
    "states/settings-gap-to-leader-footer.png",
    "states/settings-track-map-content.png",
    "states/settings-stream-chat-content.png",
    "states/settings-stream-chat-twitch.png",
    "states/settings-stream-chat-streamlabs.png",
    "states/settings-garage-cover-preview.png",
    "states/settings-fuel-calculator-content.png",
    "states/settings-fuel-calculator-header.png",
    "states/settings-fuel-calculator-footer.png",
    "states/settings-inputs-content.png",
    "states/settings-car-radar-content.png",
    "states/settings-flags-content.png",
    "states/settings-session-weather-content.png",
    "states/settings-session-weather-header.png",
    "states/settings-session-weather-footer.png",
    "states/settings-pit-service-content.png",
    "states/settings-pit-service-header.png",
    "states/settings-pit-service-footer.png",
]

WINDOWS_NATIVE_OVERLAY_SIZES = {
    "standings": (780, 520),
    "fuel-calculator": (600, 340),
    "relative": (520, 360),
    "track-map": (360, 360),
    "stream-chat": (380, 520),
    "flags": (360, 170),
    "session-weather": (480, 520),
    "pit-service": (420, 560),
    "input-state": (520, 260),
    "car-radar": (300, 300),
    "gap-to-leader": (720, 360),
}

WINDOWS_NATIVE_OVERLAY_SIZE_SOURCES = {
    "standings": "src/TmrOverlay.App/Overlays/Standings/StandingsOverlayDefinition.cs",
    "fuel-calculator": "src/TmrOverlay.App/Overlays/FuelCalculator/FuelCalculatorOverlayDefinition.cs",
    "relative": "src/TmrOverlay.App/Overlays/Relative/RelativeOverlayDefinition.cs",
    "track-map": "src/TmrOverlay.App/Overlays/TrackMap/TrackMapOverlayDefinition.cs",
    "stream-chat": "src/TmrOverlay.App/Overlays/StreamChat/StreamChatOverlayDefinition.cs",
    "flags": "src/TmrOverlay.App/Overlays/Flags/FlagsOverlayDefinition.cs",
    "session-weather": "src/TmrOverlay.App/Overlays/SessionWeather/SessionWeatherOverlayDefinition.cs",
    "pit-service": "src/TmrOverlay.App/Overlays/PitService/PitServiceOverlayDefinition.cs",
    "input-state": "src/TmrOverlay.App/Overlays/InputState/InputStateOverlayDefinition.cs",
    "car-radar": "src/TmrOverlay.App/Overlays/CarRadar/CarRadarOverlayDefinition.cs",
    "gap-to-leader": "src/TmrOverlay.App/Overlays/GapToLeader/GapToLeaderOverlayDefinition.cs",
}

PREVIEW_MODES = ("practice", "qualifying", "race")

BROWSER_REVIEW_OVERLAY_IDS = [
    "standings",
    "relative",
    "fuel-calculator",
    "session-weather",
    "pit-service",
    "input-state",
    "car-radar",
    "gap-to-leader",
    "track-map",
    "flags",
    "garage-cover",
    "stream-chat",
]

BROWSER_ONLY_OVERLAY_IDS = {
    # Garage Cover is a localhost/browser-source privacy cover controlled from
    # the Windows settings UI; the installed app does not create a native
    # WinForms overlay window for it.
    "garage-cover",
}

WINDOWS_NATIVE_OVERLAY_BODIES = {
    "standings": "table",
    "fuel-calculator": "metric-rows",
    "relative": "table",
    "track-map": "track-map",
    "stream-chat": "chat",
    "flags": "flags",
    "session-weather": "metric-rows",
    "pit-service": "metric-rows",
    "input-state": "inputs",
    "car-radar": "radar",
    "gap-to-leader": "graph",
}

BROWSER_REVIEW_OVERLAY_BODIES = {
    "standings": "table",
    "relative": "table",
    "fuel-calculator": "metrics",
    "session-weather": "metrics",
    "pit-service": "metrics",
    "input-state": "inputs",
    "car-radar": "car-radar",
    "gap-to-leader": "graph",
    "track-map": "track-map",
    "flags": "flags",
    "garage-cover": "garage-cover",
    "stream-chat": "stream-chat",
}

SEMANTIC_WAITING_EXEMPT_OVERLAYS = {
    # Stream Chat can validly render a configured/unconfigured provider state
    # without live telemetry rows.
    "stream-chat",
}

WAITING_STATUS_TOKENS = (
    "waiting for fresh",
    "waiting for telemetry",
    "waiting for timing",
    "waiting for overlay model",
    "waiting for live values",
    "waiting for player in car",
    "waiting for radar",
)

BROWSER_REVIEW_SETTINGS_PNGS = [
    "settings/general.png",
    "settings/diagnostics.png",
    "settings/general-preview-practice.png",
    "settings/general-preview-qualifying.png",
    "settings/general-preview-race.png",
    "settings/standings.png",
    "settings/standings-content.png",
    "settings/standings-header.png",
    "settings/standings-footer.png",
    "settings/relative.png",
    "settings/relative-content.png",
    "settings/relative-header.png",
    "settings/relative-footer.png",
    "settings/gap-to-leader.png",
    "settings/gap-to-leader-content.png",
    "settings/gap-to-leader-header.png",
    "settings/gap-to-leader-footer.png",
    "settings/track-map.png",
    "settings/track-map-content.png",
    "settings/stream-chat.png",
    "settings/stream-chat-content.png",
    "settings/stream-chat-twitch.png",
    "settings/stream-chat-streamlabs.png",
    "settings/garage-cover.png",
    "settings/garage-cover-preview.png",
    "settings/fuel-calculator.png",
    "settings/fuel-calculator-content.png",
    "settings/fuel-calculator-header.png",
    "settings/fuel-calculator-footer.png",
    "settings/input-state.png",
    "settings/input-state-content.png",
    "settings/car-radar.png",
    "settings/car-radar-content.png",
    "settings/flags.png",
    "settings/flags-content.png",
    "settings/session-weather.png",
    "settings/session-weather-content.png",
    "settings/session-weather-header.png",
    "settings/session-weather-footer.png",
    "settings/pit-service.png",
    "settings/pit-service-content.png",
    "settings/pit-service-header.png",
    "settings/pit-service-footer.png",
]

RELEASE_TUTORIAL_EXPECTED_PNGS = {
    "windows-release-teammate-tutorial.png": (1600, 1000),
}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default="mocks", help="Screenshot root directory.")
    parser.add_argument(
        "--profile",
        choices=("tracked", "windows-ci", "browser-review-ci", "windows-expectations", "screenshot-expectations", "release-tutorial"),
        default="tracked",
        help="Screenshot artifact profile to validate.",
    )
    parser.add_argument(
        "--min-unique-bytes",
        type=int,
        default=16,
        help="Minimum sampled unique decoded bytes before an image is treated as blank.",
    )
    args = parser.parse_args()

    root = Path(args.root)
    failures: list[str] = []
    if args.profile == "windows-ci":
        validate_windows_ci(root, args.min_unique_bytes, failures)
        return finish(failures)
    if args.profile == "browser-review-ci":
        validate_browser_review_ci(root, args.min_unique_bytes, failures)
        return finish(failures)
    if args.profile in ("windows-expectations", "screenshot-expectations"):
        validate_windows_expectations(failures)
        return finish(failures)
    if args.profile == "release-tutorial":
        validate_release_tutorial(root, args.min_unique_bytes, failures)
        return finish(failures)

    for relative_path, expected_size in EXPECTED_PNGS.items():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=expected_size,
            min_unique_bytes=args.min_unique_bytes,
            failures=failures,
        )

    for relative_path in EXPECTED_STATE_PNGS:
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=None,
            min_unique_bytes=args.min_unique_bytes,
            failures=failures,
            minimum_size=(300, 240),
        )

    for relative_path, expected_size in EXPECTED_COMPONENT_PNGS.items():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=expected_size,
            min_unique_bytes=args.min_unique_bytes,
            failures=failures,
        )

    return finish(failures)


def finish(failures: list[str]) -> int:
    if not failures:
        return 0

    print("\nScreenshot validation failed:", file=sys.stderr)
    for failure in failures:
        print(f"- {failure}", file=sys.stderr)
    return 1


def validate_windows_ci(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
    for relative_path in WINDOWS_EXPECTED_FILES:
        path = root / relative_path
        if not path.exists():
            failures.append(f"{relative_path}: missing file")

    validate_png(
        root=root,
        relative_path="contact-sheet.png",
        expected_size=None,
        min_unique_bytes=min_unique_bytes,
        failures=failures,
        minimum_size=(1200, 900),
    )

    for relative_path, expected_size in WINDOWS_EXPECTED_PNGS.items():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=expected_size,
            min_unique_bytes=WINDOWS_MIN_UNIQUE_BYTES.get(relative_path, min_unique_bytes),
            failures=failures,
        )

    for relative_path, minimum_size in WINDOWS_MINIMUM_PNGS.items():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=None,
            min_unique_bytes=WINDOWS_MIN_UNIQUE_BYTES.get(relative_path, min_unique_bytes),
            failures=failures,
            minimum_size=minimum_size,
        )

    for relative_path in WINDOWS_SETTING_REGION_PNGS:
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=None,
            min_unique_bytes=WINDOWS_MIN_UNIQUE_BYTES.get(relative_path, min_unique_bytes),
            failures=failures,
            minimum_size=(1000, 680),
        )

    for overlay_id, expected_size in WINDOWS_NATIVE_OVERLAY_SIZES.items():
        for mode in preview_modes_for_overlay(overlay_id):
            relative_path = f"native-overlays/{overlay_id}-{mode}.png"
            validate_png(
                root=root,
                relative_path=relative_path,
                expected_size=expected_size,
                min_unique_bytes=WINDOWS_MIN_UNIQUE_BYTES.get(relative_path, min_unique_bytes),
                failures=failures,
            )

    validate_windows_manifest(
        root,
        expected_paths=windows_ci_manifest_paths(),
        failures=failures,
    )


def validate_browser_review_ci(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
    manifest = root / "manifest.json"
    if not manifest.exists():
        failures.append("manifest.json: missing file")

    for relative_path in BROWSER_REVIEW_SETTINGS_PNGS:
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=None,
            min_unique_bytes=min_unique_bytes,
            failures=failures,
            minimum_size=(1000, 680),
        )

    for overlay_id in BROWSER_REVIEW_OVERLAY_IDS:
        for prefix in ("browser-overlays", "localhost-overlays"):
            validate_png(
                root=root,
                relative_path=f"{prefix}/{overlay_id}.png",
                expected_size=None,
                min_unique_bytes=min_unique_bytes,
                failures=failures,
                minimum_size=(200, 120),
            )
            for mode in preview_modes_for_overlay(overlay_id):
                validate_png(
                    root=root,
                    relative_path=f"{prefix}/{overlay_id}-{mode}.png",
                    expected_size=None,
                    min_unique_bytes=min_unique_bytes,
                    failures=failures,
                    minimum_size=(200, 120),
                )

    validate_browser_review_manifest(
        root,
        expected_paths=browser_review_manifest_paths(),
        failures=failures,
    )


def windows_ci_manifest_paths() -> set[str]:
    paths = set(WINDOWS_EXPECTED_PNGS) | set(WINDOWS_MINIMUM_PNGS) | set(WINDOWS_SETTING_REGION_PNGS)
    paths.update(f"components/settings/{path}" for path in EXPECTED_WINDOWS_COMPONENT_FILES())
    for overlay_id in WINDOWS_NATIVE_OVERLAY_SIZES:
        for mode in preview_modes_for_overlay(overlay_id):
            paths.add(f"native-overlays/{overlay_id}-{mode}.png")
    return paths


def EXPECTED_WINDOWS_COMPONENT_FILES() -> tuple[str, ...]:
    return (
        "sidebar-tabs.png",
        "region-tabs.png",
        "unit-choice.png",
        "overlay-controls.png",
        "content-matrix.png",
        "chat-inputs.png",
        "support-buttons.png",
        "browser-source.png",
    )


def browser_review_manifest_paths() -> set[str]:
    paths = set(BROWSER_REVIEW_SETTINGS_PNGS)
    for overlay_id in BROWSER_REVIEW_OVERLAY_IDS:
        for prefix in ("browser-overlays", "localhost-overlays"):
            paths.add(f"{prefix}/{overlay_id}.png")
            for mode in preview_modes_for_overlay(overlay_id):
                paths.add(f"{prefix}/{overlay_id}-{mode}.png")
    return paths


def validate_windows_manifest(root: Path, expected_paths: set[str], failures: list[str]) -> None:
    manifest = read_manifest(root, failures)
    if manifest is None:
        return

    screenshots = manifest_screenshots(manifest, failures)
    if screenshots is None:
        return

    compare_sets("Windows screenshot manifest paths", set(screenshots), expected_paths, failures)
    for path, screenshot in screenshots.items():
        metadata = screenshot.get("metadata")
        if not isinstance(metadata, dict):
            failures.append(f"{path}: manifest missing metadata object")
            continue
        require_manifest_fields(path, metadata, ["surface", "renderer"], failures)
        if path.startswith("native-overlays/"):
            require_manifest_fields(path, metadata, ["overlayId", "previewMode", "fixture", "sourceContract", "status", "evidence", "body"], failures)
            if metadata.get("surface") != "windows-native-overlay":
                failures.append(f"{path}: expected windows-native-overlay surface, got {metadata.get('surface')!r}")
            validate_overlay_semantics(
                path,
                values=metadata,
                overlay_id=metadata.get("overlayId"),
                body_field="body",
                expected_bodies=WINDOWS_NATIVE_OVERLAY_BODIES,
                failures=failures,
            )
        if path.startswith("states/settings-"):
            require_manifest_fields(path, metadata, ["tab", "region", "fixture", "sourceContract"], failures)


def validate_browser_review_manifest(root: Path, expected_paths: set[str], failures: list[str]) -> None:
    manifest = read_manifest(root, failures)
    if manifest is None:
        return

    screenshots = manifest_screenshots(manifest, failures)
    if screenshots is None:
        return

    compare_sets("Browser review screenshot manifest paths", set(screenshots), expected_paths, failures)
    for path, screenshot in screenshots.items():
        require_manifest_fields(path, screenshot, ["surface", "renderer", "sourceContract"], failures)
        if path.startswith(("browser-overlays/", "localhost-overlays/")):
            require_manifest_fields(path, screenshot, ["overlayId", "previewMode", "moduleAsset", "status", "bodyKind"], failures)
            validate_overlay_semantics(
                path,
                values=screenshot,
                overlay_id=screenshot.get("overlayId"),
                body_field="bodyKind",
                expected_bodies=BROWSER_REVIEW_OVERLAY_BODIES,
                failures=failures,
            )
        if path.startswith("settings/"):
            require_manifest_fields(path, screenshot, ["tab", "region"], failures)


def validate_overlay_semantics(
    path: str,
    values: dict[str, object],
    overlay_id: object,
    body_field: str,
    expected_bodies: dict[str, str],
    failures: list[str],
) -> None:
    if not isinstance(overlay_id, str) or not overlay_id:
        return

    expected_body = expected_bodies.get(overlay_id)
    actual_body = values.get(body_field)
    if expected_body is not None and actual_body != expected_body:
        failures.append(f"{path}: expected {body_field} {expected_body!r}, got {actual_body!r}")

    status = str(values.get("status") or "").strip().lower()
    if overlay_id not in SEMANTIC_WAITING_EXEMPT_OVERLAYS:
        for token in WAITING_STATUS_TOKENS:
            if token in status:
                failures.append(f"{path}: manifest status {status!r} indicates the preview rendered a waiting state")
                break

    if overlay_id == "flags":
        if status in ("", "none", "waiting"):
            failures.append(f"{path}: flags preview did not expose any active flags")
        flag_count = values.get("flagCount")
        if isinstance(flag_count, int) and flag_count <= 0:
            failures.append(f"{path}: flags preview model contains no visible flags")

    if overlay_id == "car-radar" and values.get("radarShouldRender") is False:
        failures.append(f"{path}: car radar preview model reported radarShouldRender=false")

    if values.get("shouldRender") is False and overlay_id not in SEMANTIC_WAITING_EXEMPT_OVERLAYS:
        failures.append(f"{path}: preview model reported shouldRender=false")


def read_manifest(root: Path, failures: list[str]) -> Optional[dict[str, object]]:
    path = root / "manifest.json"
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except OSError as exc:
        failures.append(f"manifest.json: {exc}")
    except json.JSONDecodeError as exc:
        failures.append(f"manifest.json: invalid JSON: {exc}")
    return None


def manifest_screenshots(
    manifest: dict[str, object],
    failures: list[str],
) -> Optional[dict[str, dict[str, object]]]:
    screenshots = manifest.get("screenshots")
    if not isinstance(screenshots, list):
        failures.append("manifest.json: screenshots must be a list")
        return None

    indexed: dict[str, dict[str, object]] = {}
    for index, screenshot in enumerate(screenshots):
        if not isinstance(screenshot, dict):
            failures.append(f"manifest.json: screenshots[{index}] must be an object")
            continue
        path = screenshot.get("path")
        if not isinstance(path, str) or not path:
            failures.append(f"manifest.json: screenshots[{index}] missing path")
            continue
        indexed[path] = screenshot
    return indexed


def require_manifest_fields(
    path: str,
    values: dict[str, object],
    fields: list[str],
    failures: list[str],
) -> None:
    for field in fields:
        if values.get(field) in (None, ""):
            failures.append(f"{path}: manifest missing {field}")


def validate_windows_expectations(failures: list[str]) -> None:
    repo_root = Path(__file__).resolve().parents[1]
    validate_screenshot_coverage_contracts(repo_root, failures)

    covered_paths = set(WINDOWS_EXPECTED_SIZE_SOURCES) | set(WINDOWS_GENERATOR_SIZE_SOURCES)
    for relative_path in sorted(set(WINDOWS_EXPECTED_PNGS) - covered_paths):
        failures.append(f"{relative_path}: missing Windows screenshot expectation source contract")
    for relative_path in sorted(covered_paths - set(WINDOWS_EXPECTED_PNGS)):
        failures.append(f"{relative_path}: source contract exists without a Windows expected PNG entry")

    for relative_path, source_path in WINDOWS_EXPECTED_SIZE_SOURCES.items():
        expected_size = WINDOWS_EXPECTED_PNGS.get(relative_path)
        if expected_size is None:
            continue
        actual_size = read_overlay_definition_size(repo_root / source_path, source_path, failures)
        if actual_size is None:
            continue
        validate_expected_size_contract(relative_path, expected_size, actual_size, source_path, failures)

    for relative_path, (source_path, pattern) in WINDOWS_GENERATOR_SIZE_SOURCES.items():
        expected_size = WINDOWS_EXPECTED_PNGS.get(relative_path)
        if expected_size is None:
            continue
        actual_size = read_generator_size(repo_root / source_path, source_path, pattern, failures)
        if actual_size is None:
            continue
        validate_expected_size_contract(relative_path, expected_size, actual_size, source_path, failures)

    for overlay_id, source_path in WINDOWS_NATIVE_OVERLAY_SIZE_SOURCES.items():
        expected_size = WINDOWS_NATIVE_OVERLAY_SIZES.get(overlay_id)
        if expected_size is None:
            continue
        actual_size = read_overlay_definition_size(repo_root / source_path, source_path, failures)
        if actual_size is None:
            continue
        validate_expected_size_contract(f"native-overlays/{overlay_id}-*.png", expected_size, actual_size, source_path, failures)


def validate_screenshot_coverage_contracts(repo_root: Path, failures: list[str]) -> None:
    overlay_ids = discover_overlay_definition_ids(repo_root, failures)
    if not overlay_ids:
        return
    native_overlay_ids = set(overlay_ids) - BROWSER_ONLY_OVERLAY_IDS

    compare_sets(
        "WINDOWS_NATIVE_OVERLAY_SIZES",
        set(WINDOWS_NATIVE_OVERLAY_SIZES),
        native_overlay_ids,
        failures,
    )
    compare_sets(
        "WINDOWS_NATIVE_OVERLAY_SIZE_SOURCES",
        set(WINDOWS_NATIVE_OVERLAY_SIZE_SOURCES),
        native_overlay_ids,
        failures,
    )
    compare_sets(
        "BROWSER_REVIEW_OVERLAY_IDS",
        set(BROWSER_REVIEW_OVERLAY_IDS),
        set(overlay_ids),
        failures,
    )

    compare_sets(
        "Windows settings screenshot expectations",
        set(WINDOWS_SETTING_REGION_PNGS) | {
            path for path in WINDOWS_MINIMUM_PNGS
            if path.startswith("states/settings-")
        },
        expected_windows_settings_pngs(overlay_ids),
        failures,
    )
    compare_sets(
        "Browser review settings screenshot expectations",
        set(BROWSER_REVIEW_SETTINGS_PNGS),
        expected_browser_review_settings_pngs(overlay_ids),
        failures,
    )


def discover_overlay_definition_ids(repo_root: Path, failures: list[str]) -> list[str]:
    ids: list[str] = []
    for path in sorted((repo_root / "src" / "TmrOverlay.App" / "Overlays").glob("*/*OverlayDefinition.cs")):
        if "SettingsPanel" in path.parts:
            continue
        try:
            content = path.read_text(encoding="utf-8")
        except OSError as exc:
            failures.append(f"{path.relative_to(repo_root)}: {exc}")
            continue

        match = re.search(r'\bId:\s*"([^"]+)"', content)
        if match is None:
            failures.append(f"{path.relative_to(repo_root)}: could not find OverlayDefinition Id")
            continue
        ids.append(match.group(1))

    return ids


def compare_sets(
    label: str,
    actual: set[str],
    expected: set[str],
    failures: list[str],
) -> None:
    for value in sorted(expected - actual):
        failures.append(f"{label}: missing {value}")
    for value in sorted(actual - expected):
        failures.append(f"{label}: stale {value}")


def expected_windows_settings_pngs(overlay_ids: list[str]) -> set[str]:
    paths = {
        "states/settings-general.png",
        "states/settings-support.png",
        *(f"states/settings-general-preview-{mode}.png" for mode in PREVIEW_MODES),
    }
    for overlay_id in overlay_ids:
        stem = "inputs" if overlay_id == "input-state" else overlay_id
        for region in regions_for_overlay(overlay_id):
            suffix = "" if region == "general" else f"-{region}"
            paths.add(f"states/settings-{stem}{suffix}.png")
    return paths


def expected_browser_review_settings_pngs(overlay_ids: list[str]) -> set[str]:
    paths = {
        "settings/general.png",
        "settings/diagnostics.png",
        *(f"settings/general-preview-{mode}.png" for mode in PREVIEW_MODES),
    }
    for overlay_id in overlay_ids:
        for region in regions_for_overlay(overlay_id):
            suffix = "" if region == "general" else f"-{region}"
            paths.add(f"settings/{overlay_id}{suffix}.png")
    return paths


def regions_for_overlay(overlay_id: str) -> tuple[str, ...]:
    if overlay_id == "garage-cover":
        return ("general", "preview")
    if overlay_id == "stream-chat":
        return ("general", "content", "twitch", "streamlabs")
    if overlay_id in {
        "standings",
        "relative",
        "fuel-calculator",
        "gap-to-leader",
        "session-weather",
        "pit-service",
    }:
        return ("general", "content", "header", "footer")
    return ("general", "content")


def preview_modes_for_overlay(overlay_id: str) -> tuple[str, ...]:
    return ("race",) if overlay_id == "gap-to-leader" else PREVIEW_MODES


def read_overlay_definition_size(
    path: Path,
    display_path: str,
    failures: list[str],
) -> Optional[tuple[int, int]]:
    try:
        content = path.read_text(encoding="utf-8")
    except OSError as exc:
        failures.append(f"{display_path}: {exc}")
        return None

    width_match = re.search(r"\bDefaultWidth:\s*(\d+)", content)
    height_match = re.search(r"\bDefaultHeight:\s*(\d+)", content)
    if width_match is None or height_match is None:
        failures.append(f"{display_path}: could not find DefaultWidth/DefaultHeight")
        return None

    return int(width_match.group(1)), int(height_match.group(1))


def read_generator_size(
    path: Path,
    display_path: str,
    pattern: str,
    failures: list[str],
) -> Optional[tuple[int, int]]:
    try:
        content = path.read_text(encoding="utf-8")
    except OSError as exc:
        failures.append(f"{display_path}: {exc}")
        return None

    match = re.search(pattern, content, flags=re.DOTALL)
    if match is None:
        failures.append(f"{display_path}: could not find Windows screenshot generator size contract")
        return None

    return int(match.group(1)), int(match.group(2))


def validate_expected_size_contract(
    relative_path: str,
    expected_size: tuple[int, int],
    actual_size: tuple[int, int],
    source_path: str,
    failures: list[str],
) -> None:
    if expected_size != actual_size:
        failures.append(
            f"{relative_path}: WINDOWS_EXPECTED_PNGS says {expected_size[0]}x{expected_size[1]}, "
            f"but {source_path} declares {actual_size[0]}x{actual_size[1]}"
        )
        return

    print(f"ok {relative_path}: expectation matches {source_path} at {expected_size[0]}x{expected_size[1]}")


def validate_release_tutorial(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
    for relative_path, expected_size in RELEASE_TUTORIAL_EXPECTED_PNGS.items():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=expected_size,
            min_unique_bytes=min_unique_bytes,
            failures=failures,
        )


def validate_png(
    root: Path,
    relative_path: str,
    expected_size: Optional[tuple[int, int]],
    min_unique_bytes: int,
    failures: list[str],
    minimum_size: Optional[tuple[int, int]] = None,
) -> None:
    path = root / relative_path
    try:
        metadata = inspect_png(path, min_unique_bytes)
    except Exception as exc:  # noqa: BLE001 - this is a CLI validation boundary.
        failures.append(f"{relative_path}: {exc}")
        return

    size = metadata["size"]
    if expected_size is not None and size != expected_size:
        failures.append(
            f"{relative_path}: expected {expected_size[0]}x{expected_size[1]}, "
            f"got {size[0]}x{size[1]}"
        )
    if minimum_size is not None and (size[0] < minimum_size[0] or size[1] < minimum_size[1]):
        failures.append(
            f"{relative_path}: expected at least {minimum_size[0]}x{minimum_size[1]}, "
            f"got {size[0]}x{size[1]}"
        )
    if metadata["unique_bytes"] < min_unique_bytes:
        failures.append(
            f"{relative_path}: only {metadata['unique_bytes']} sampled decoded bytes; "
            "image may be blank"
        )
    if metadata["byte_range"] < 24:
        failures.append(
            f"{relative_path}: decoded byte range {metadata['byte_range']}; "
            "image may be blank or unreadable"
        )

    print(
        f"ok {relative_path}: {size[0]}x{size[1]}, "
        f"{metadata['unique_bytes']}+ decoded bytes, byte range {metadata['byte_range']}"
    )


def inspect_png(path: Path, min_unique_bytes: int) -> dict[str, object]:
    if not path.exists():
        raise FileNotFoundError("missing PNG")

    width, height, bit_depth, color_type, compressed = read_png_chunks(path)
    if bit_depth != 8:
        raise ValueError(f"unsupported bit depth {bit_depth}")

    channels = channel_count(color_type)
    row_width = width * channels
    raw = zlib.decompress(compressed)
    expected_raw_len = height * (row_width + 1)
    if len(raw) != expected_raw_len:
        raise ValueError(f"unexpected decoded byte length {len(raw)} != {expected_raw_len}")

    sample_stride = max(1, len(raw) // 250_000)
    sample = raw[::sample_stride]
    unique_bytes = len(set(sample))

    return {
        "size": (width, height),
        "unique_bytes": unique_bytes,
        "byte_range": max(sample) - min(sample) if sample else 0,
    }


def read_png_chunks(path: Path) -> tuple[int, int, int, int, bytes]:
    data = path.read_bytes()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("not a PNG")

    cursor = 8
    width = height = bit_depth = color_type = None
    idat_parts: list[bytes] = []
    while cursor < len(data):
        if cursor + 8 > len(data):
            raise ValueError("truncated chunk header")
        length = struct.unpack(">I", data[cursor:cursor + 4])[0]
        chunk_type = data[cursor + 4:cursor + 8]
        chunk_data = data[cursor + 8:cursor + 8 + length]
        cursor += 12 + length

        if chunk_type == b"IHDR":
            width, height, bit_depth, color_type = struct.unpack(">IIBB", chunk_data[:10])
        elif chunk_type == b"IDAT":
            idat_parts.append(chunk_data)
        elif chunk_type == b"IEND":
            break

    if None in (width, height, bit_depth, color_type):
        raise ValueError("missing IHDR")
    if not idat_parts:
        raise ValueError("missing IDAT")
    return width, height, bit_depth, color_type, b"".join(idat_parts)


def channel_count(color_type: int) -> int:
    if color_type == 0:
        return 1
    if color_type == 2:
        return 3
    if color_type == 3:
        return 1
    if color_type == 4:
        return 2
    if color_type == 6:
        return 4
    raise ValueError(f"unsupported color type {color_type}")

if __name__ == "__main__":
    raise SystemExit(main())
