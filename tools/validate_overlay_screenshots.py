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

WINDOWS_NATIVE_SPECIAL_PNGS = {
    "native-overlays/standings-preview-sizing-race.png": (589, 520),
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

LOCALHOST_OVERLAY_ALIASES = {
    "fuel-calculator": (("calculator", "/overlays/calculator"),),
    "input-state": (("inputs", "/overlays/inputs"),),
}

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
        choices=(
            "tracked",
            "windows-ci",
            "browser-review-ci",
            "localhost-ci",
            "browser-localhost-ci",
            "windows-expectations",
            "screenshot-expectations",
            "release-tutorial",
        ),
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
    if args.profile == "localhost-ci":
        validate_localhost_ci(root, args.min_unique_bytes, failures)
        return finish(failures)
    if args.profile == "browser-localhost-ci":
        validate_browser_localhost_ci(root, args.min_unique_bytes, failures)
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

    for relative_path, expected_size in WINDOWS_NATIVE_SPECIAL_PNGS.items():
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

    validate_web_overlay_pngs(root, "browser-overlays", min_unique_bytes, failures)

    validate_browser_review_manifest(
        root,
        expected_paths=browser_review_manifest_paths(),
        label="Browser review screenshot manifest paths",
        failures=failures,
    )


def validate_localhost_ci(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
    manifest = root / "manifest.json"
    if not manifest.exists():
        failures.append("manifest.json: missing file")

    validate_web_overlay_pngs(root, "localhost-overlays", min_unique_bytes, failures)
    validate_localhost_alias_pngs(root, min_unique_bytes, failures)

    validate_browser_review_manifest(
        root,
        expected_paths=localhost_manifest_paths(),
        label="Localhost screenshot manifest paths",
        failures=failures,
    )


def validate_browser_localhost_ci(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
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

    validate_web_overlay_pngs(root, "browser-overlays", min_unique_bytes, failures)
    validate_web_overlay_pngs(root, "localhost-overlays", min_unique_bytes, failures)
    validate_localhost_alias_pngs(root, min_unique_bytes, failures)

    validate_browser_review_manifest(
        root,
        expected_paths=browser_localhost_manifest_paths(),
        label="Browser/localhost screenshot manifest paths",
        failures=failures,
    )


def validate_web_overlay_pngs(root: Path, prefix: str, min_unique_bytes: int, failures: list[str]) -> None:
    for overlay_id in BROWSER_REVIEW_OVERLAY_IDS:
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


def validate_localhost_alias_pngs(root: Path, min_unique_bytes: int, failures: list[str]) -> None:
    for relative_path in localhost_alias_manifest_paths():
        validate_png(
            root=root,
            relative_path=relative_path,
            expected_size=None,
            min_unique_bytes=min_unique_bytes,
            failures=failures,
            minimum_size=(200, 120),
        )


def windows_ci_manifest_paths() -> set[str]:
    paths = set(WINDOWS_EXPECTED_PNGS) | set(WINDOWS_MINIMUM_PNGS) | set(WINDOWS_SETTING_REGION_PNGS) | set(WINDOWS_NATIVE_SPECIAL_PNGS)
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
    paths.update(web_overlay_manifest_paths("browser-overlays"))
    return paths


def localhost_manifest_paths() -> set[str]:
    return web_overlay_manifest_paths("localhost-overlays") | localhost_alias_manifest_paths()


def browser_localhost_manifest_paths() -> set[str]:
    return browser_review_manifest_paths() | localhost_manifest_paths()


def web_overlay_manifest_paths(prefix: str) -> set[str]:
    paths = set()
    for overlay_id in BROWSER_REVIEW_OVERLAY_IDS:
        paths.add(f"{prefix}/{overlay_id}.png")
        for mode in preview_modes_for_overlay(overlay_id):
            paths.add(f"{prefix}/{overlay_id}-{mode}.png")
    return paths


def localhost_alias_manifest_paths() -> set[str]:
    paths = set()
    for overlay_id, aliases in LOCALHOST_OVERLAY_ALIASES.items():
        for alias_slug, _alias_route in aliases:
            stem = f"localhost-overlays/{overlay_id}-alias-{alias_slug}"
            paths.add(f"{stem}.png")
            for mode in preview_modes_for_overlay(overlay_id):
                paths.add(f"{stem}-{mode}.png")
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
        require_manifest_fields(path, screenshot, ["textSample", "contentBounds", "layout", "scenarioEvidence"], failures)
        require_rect(path, screenshot.get("contentBounds"), "screenshot content bounds", failures)
        require_layout_evidence(path, screenshot.get("layout"), failures)
        require_scenario_evidence(path, screenshot.get("scenarioEvidence"), failures)
        if path.startswith(("states/settings-", "components/settings/")):
            require_manifest_fields(path, screenshot, ["uiEvidence"], failures)
            require_settings_ui_evidence(path, screenshot.get("uiEvidence"), failures)
        if path.startswith("native-overlays/"):
            require_manifest_fields(
                path,
                screenshot,
                [
                    "surface",
                    "renderer",
                    "sourceContract",
                    "overlayId",
                    "previewMode",
                    "status",
                    "bytes",
                    "source",
                    "bodyKind",
                    "textSample",
                    "contentBounds",
                ],
                failures,
            )
            require_manifest_fields(path, metadata, ["overlayId", "previewMode", "fixture", "sourceContract", "status", "evidence", "body"], failures)
            require_windows_native_comparison_evidence(path, screenshot, failures)
            require_layout_evidence(path, metadata.get("layout"), failures)
            require_model_evidence(path, screenshot.get("modelEvidence"), failures)
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
            validate_overlay_semantics(
                path,
                values=screenshot,
                overlay_id=screenshot.get("overlayId"),
                body_field="bodyKind",
                expected_bodies=BROWSER_REVIEW_OVERLAY_BODIES,
                failures=failures,
            )
        if path.startswith("states/settings-"):
            require_manifest_fields(path, metadata, ["tab", "region", "fixture", "sourceContract"], failures)


def validate_browser_review_manifest(
    root: Path,
    expected_paths: set[str],
    label: str,
    failures: list[str],
) -> None:
    manifest = read_manifest(root, failures)
    if manifest is None:
        return

    screenshots = manifest_screenshots(manifest, failures)
    if screenshots is None:
        return

    compare_sets(label, set(screenshots), expected_paths, failures)
    for path, screenshot in screenshots.items():
        require_manifest_fields(path, screenshot, ["surface", "renderer", "sourceContract"], failures)
        require_layout_evidence(path, screenshot.get("layout"), failures)
        require_manifest_fields(path, screenshot, ["scenarioEvidence"], failures)
        require_scenario_evidence(path, screenshot.get("scenarioEvidence"), failures)
        if path.startswith(("browser-overlays/", "localhost-overlays/")):
            require_manifest_fields(path, screenshot, ["overlayId", "previewMode", "moduleAsset", "status", "bodyKind"], failures)
            require_model_evidence(path, screenshot.get("modelEvidence"), failures)
            validate_localhost_alias_manifest(path, screenshot, failures)
            validate_overlay_semantics(
                path,
                values=screenshot,
                overlay_id=screenshot.get("overlayId"),
                body_field="bodyKind",
                expected_bodies=BROWSER_REVIEW_OVERLAY_BODIES,
                failures=failures,
            )
        if path.startswith("settings/"):
            require_manifest_fields(path, screenshot, ["tab", "region", "uiEvidence"], failures)
            require_settings_ui_evidence(path, screenshot.get("uiEvidence"), failures)
            validate_settings_region_manifest(path, screenshot, failures)


def require_layout_evidence(path: str, value: object, failures: list[str]) -> None:
    if not isinstance(value, dict):
        failures.append(f"{path}: manifest missing layout evidence")
        return

    contract = value.get("contract") or value.get("Contract")
    if not isinstance(contract, str) or not contract:
        failures.append(f"{path}: layout evidence missing contract")

    root = value.get("root") or value.get("Root") or value.get("client") or value.get("Client")
    if not isinstance(root, dict):
        failures.append(f"{path}: layout evidence missing root/client bounds")

    elements = value.get("elements") or value.get("Elements")
    body_layout = value.get("bodyLayout") or value.get("BodyLayout")
    if elements is None and body_layout is None:
        failures.append(f"{path}: layout evidence missing elements/bodyLayout details")
        return

    if isinstance(body_layout, dict):
        require_native_body_layout_evidence(path, body_layout, failures)


def require_scenario_evidence(path: str, value: object, failures: list[str]) -> None:
    if not isinstance(value, dict):
        failures.append(f"{path}: manifest missing scenario evidence")
        return

    contract = value.get("contract")
    if not isinstance(contract, str) or not contract:
        failures.append(f"{path}: scenario evidence missing contract")

    if value.get("scenarioHash") in (None, ""):
        failures.append(f"{path}: scenario evidence missing scenarioHash")
    if value.get("sourceHash") in (None, ""):
        failures.append(f"{path}: scenario evidence missing sourceHash")

    source_files = value.get("sourceFiles")
    if not isinstance(source_files, list):
        failures.append(f"{path}: scenario evidence missing sourceFiles list")
        return

    for index, source_file in enumerate(source_files):
        if not isinstance(source_file, dict):
            failures.append(f"{path}: scenario source file {index} is not an object")
            continue
        if source_file.get("path") in (None, ""):
            failures.append(f"{path}: scenario source file {index} missing path")
        if source_file.get("exists") is True:
            require_positive_number(path, source_file.get("bytes"), f"scenario source file {index} bytes", failures)
            if source_file.get("sha256") in (None, ""):
                failures.append(f"{path}: scenario source file {index} missing sha256")


def require_settings_ui_evidence(path: str, value: object, failures: list[str]) -> None:
    if not isinstance(value, dict):
        failures.append(f"{path}: settings UI evidence missing object")
        return

    contract = value.get("contract")
    if not isinstance(contract, str) or not contract:
        failures.append(f"{path}: settings UI evidence missing contract")

    require_rect(path, value.get("root"), "settings UI root", failures)
    require_rect(path, value.get("contentBounds"), "settings UI content bounds", failures)

    is_component_crop = path.startswith("components/settings/")
    tabs = value.get("tabs")
    if not is_component_crop and (not isinstance(tabs, list) or not tabs):
        failures.append(f"{path}: settings UI evidence missing sidebar tabs")
    elif isinstance(tabs, list):
        for index, tab in enumerate(tabs[:16]):
            if not isinstance(tab, dict):
                continue
            if tab.get("text") in (None, ""):
                failures.append(f"{path}: settings UI tab {index} missing text")
            require_rect(path, tab.get("bounds"), f"settings UI tab {index} bounds", failures)

    tab = value.get("tab")
    requested_region = value.get("requestedRegion")
    if tab not in (None, "general", "support", "error-logging") and requested_region not in (None, "general"):
        regions = value.get("regions")
        if not isinstance(regions, list) or not regions:
            failures.append(f"{path}: settings UI evidence missing region controls")

    panels = value.get("panels")
    if not is_component_crop and (not isinstance(panels, list) or not panels):
        failures.append(f"{path}: settings UI evidence missing panel bounds")
    elif isinstance(panels, list):
        for index, panel in enumerate(panels[:12]):
            if not isinstance(panel, dict):
                continue
            require_rect(path, panel.get("bounds"), f"settings UI panel {index} bounds", failures)

    controls = value.get("controls")
    if controls is not None and not isinstance(controls, list):
        failures.append(f"{path}: settings UI controls is not a list")
    elif isinstance(controls, list):
        for index, control in enumerate(controls[:32]):
            if not isinstance(control, dict):
                continue
            require_rect(path, control.get("bounds"), f"settings UI control {index} bounds", failures)

    if is_component_crop:
        evidence_counts = [
            len(value.get("tabs")) if isinstance(value.get("tabs"), list) else 0,
            len(value.get("regions")) if isinstance(value.get("regions"), list) else 0,
            len(value.get("panels")) if isinstance(value.get("panels"), list) else 0,
            len(value.get("controls")) if isinstance(value.get("controls"), list) else 0,
        ]
        if max(evidence_counts, default=0) <= 0:
            failures.append(f"{path}: settings component UI evidence did not capture any structural items")


def require_model_evidence(path: str, value: object, failures: list[str]) -> None:
    if not isinstance(value, dict):
        failures.append(f"{path}: manifest missing model layout evidence")
        return

    contract = value.get("contract")
    if not isinstance(contract, str) or not contract:
        failures.append(f"{path}: model layout evidence missing contract")

    body_kind = value.get("bodyKind")
    if not isinstance(body_kind, str) or not body_kind:
        failures.append(f"{path}: model layout evidence missing bodyKind")
        return

    if body_kind == "table":
        require_non_empty_list(path, value, "columns", failures)
        require_rows_with_cells(path, value.get("rows"), "model table rows", failures)
        require_rendered_cell_evidence(path, value.get("rows"), failures)
    elif body_kind == "metrics":
        if not any(non_empty_list(value.get(field)) for field in ("metrics", "metricSections", "gridSections")):
            failures.append(f"{path}: model metric evidence missing metrics/sections")
        require_metric_text_evidence(path, value.get("metrics"), failures)
        require_metric_section_text_evidence(path, value.get("metricSections"), failures)
        require_grid_section_text_evidence(path, value.get("gridSections"), failures)
    elif body_kind == "graph":
        graph = value.get("graph")
        if not isinstance(graph, dict):
            failures.append(f"{path}: model graph evidence missing graph object")
        else:
            geometry = graph.get("geometry")
            if not isinstance(geometry, dict):
                failures.append(f"{path}: model graph evidence missing rendered geometry")
            else:
                require_rect(path, geometry.get("frame"), "model graph frame", failures)
                require_rect(path, geometry.get("plot"), "model graph plot", failures)
                require_rect(path, geometry.get("labelLane"), "model graph label lane", failures)
                require_non_empty_list(path, geometry, "series", failures)
                for index, series in enumerate(geometry.get("series") if isinstance(geometry.get("series"), list) else []):
                    if not isinstance(series, dict):
                        continue
                    require_non_empty_list(f"{path}: graph series {index}", series, "points", failures)
                    if not series.get("baseColor"):
                        failures.append(f"{path}: graph series {index} missing baseColor")
                    if series.get("strokeWidth") in (None, ""):
                        failures.append(f"{path}: graph series {index} missing strokeWidth")
                    if not series.get("endpointLabel"):
                        failures.append(f"{path}: graph series {index} missing endpointLabel")
                require_list_key(path, geometry, "metricRows", failures)
    elif body_kind == "inputs":
        inputs = value.get("inputs")
        if not isinstance(inputs, dict):
            failures.append(f"{path}: model input evidence missing inputs object")
        elif inputs.get("hasGraph") is True:
            graph = inputs.get("graph")
            if not isinstance(graph, dict):
                failures.append(f"{path}: model input evidence missing graph geometry")
            else:
                require_rect(path, graph.get("bounds"), "model input graph bounds", failures)
                require_non_empty_list(path, graph, "gridLines", failures)
                require_non_empty_list(path, graph, "series", failures)
    elif body_kind in ("car-radar", "track-map"):
        key = "carRadar" if body_kind == "car-radar" else "trackMap"
        vector = value.get(key)
        if not isinstance(vector, dict):
            failures.append(f"{path}: model {body_kind} evidence missing {key} object")
        else:
            if path.startswith("native-overlays/") or vector.get("width") not in (None, ""):
                require_positive_number(path, vector.get("width"), f"model {body_kind} source width", failures)
            if path.startswith("native-overlays/") or vector.get("height") not in (None, ""):
                require_positive_number(path, vector.get("height"), f"model {body_kind} source height", failures)
            if body_kind == "car-radar":
                if vector.get("shouldRender") is not True:
                    failures.append(f"{path}: model car-radar evidence did not prove shouldRender=true")
                require_non_negative_int(path, vector.get("carCount"), "model car-radar carCount", failures)
                require_non_negative_int(path, vector.get("labelCount"), "model car-radar labelCount", failures)
                if isinstance(vector.get("carCount"), int) and vector.get("carCount") > 0:
                    require_non_empty_list(path, vector, "items", failures)
                if isinstance(vector.get("labelCount"), int) and vector.get("labelCount") > 0:
                    require_non_empty_list(path, vector, "labels", failures)
            else:
                require_non_negative_int(path, vector.get("markerCount"), "model track-map markerCount", failures)
                require_non_negative_int(path, vector.get("primitiveCount"), "model track-map primitiveCount", failures)
                if isinstance(vector.get("markerCount"), int) and vector.get("markerCount") > 0:
                    require_non_empty_list(path, vector, "items", failures)
                if isinstance(vector.get("primitiveCount"), int) and vector.get("primitiveCount") > 0:
                    require_non_empty_list(path, vector, "primitives", failures)
            if get_manifest_value(vector, "itemCount") not in (None, 0):
                require_non_empty_list(path, vector, "items", failures)
                require_vector_item_evidence(path, get_manifest_value(vector, "items"), failures)
            if "primitives" in vector:
                require_vector_primitive_evidence(path, vector.get("primitives"), failures)
            if "labels" in vector:
                require_vector_label_evidence(path, vector.get("labels"), failures)
    elif body_kind == "flags":
        flags = value.get("flags")
        if not isinstance(flags, dict) or not non_empty_list(flags.get("kinds")):
            failures.append(f"{path}: model flags evidence missing flag kinds")
        elif non_empty_list(flags.get("cells")):
            for index, cell in enumerate(flags.get("cells") if isinstance(flags.get("cells"), list) else []):
                if not isinstance(cell, dict):
                    continue
                if not cell.get("kind"):
                    failures.append(f"{path}: model flag cell {index} missing kind")
                require_rect(path, cell.get("bounds"), f"model flag cell {index} bounds", failures)
                require_rect(path, cell.get("clothBounds"), f"model flag cell {index} cloth bounds", failures)


def require_native_body_layout_evidence(path: str, body_layout: dict[str, object], failures: list[str]) -> None:
    kind = body_layout.get("kind") or body_layout.get("Kind")
    if not isinstance(kind, str) or not kind:
        failures.append(f"{path}: native body layout missing kind")
        return

    if kind == "table":
        require_non_empty_list(path, body_layout, "columns", failures)
        require_rows_with_cells(path, get_manifest_value(body_layout, "rows"), "native table rows", failures)
    elif kind == "metric-rows":
        if not any(non_empty_list(body_layout.get(field)) for field in ("metricRows", "metricGrids", "MetricRows", "MetricGrids")):
            failures.append(f"{path}: native metric layout missing metric rows/grids")
        require_metric_text_evidence(path, get_manifest_value(body_layout, "metricRows"), failures)
    elif kind == "graph":
        graph = body_layout.get("graph") or body_layout.get("Graph")
        if not isinstance(graph, dict):
            failures.append(f"{path}: native graph layout missing graph object")
            return
        require_rect(path, get_manifest_value(graph, "frame"), "native graph frame", failures)
        require_rect(path, get_manifest_value(graph, "plot"), "native graph plot", failures)
        require_rect(path, get_manifest_value(graph, "labelLane"), "native graph label lane", failures)
        require_non_empty_list(path, graph, "series", failures)
        graph_series = get_manifest_value(graph, "series")
        for index, series in enumerate(graph_series if isinstance(graph_series, list) else []):
            if not isinstance(series, dict):
                continue
            require_non_empty_list(f"{path}: native graph series {index}", series, "points", failures)
            if not get_manifest_value(series, "baseColor"):
                failures.append(f"{path}: native graph series {index} missing baseColor")
            if get_manifest_value(series, "strokeWidth") in (None, ""):
                failures.append(f"{path}: native graph series {index} missing strokeWidth")
        require_list_key(path, graph, "metricRows", failures)
    elif kind == "inputs":
        inputs = body_layout.get("inputs") or body_layout.get("Inputs")
        if not isinstance(inputs, dict):
            failures.append(f"{path}: native input layout missing inputs object")
            return
        if inputs.get("graph") is not None or inputs.get("Graph") is not None:
            require_non_empty_list(path, inputs, "gridLines", failures)
            require_non_empty_list(path, inputs, "traceSeries", failures)
    elif kind in ("radar", "track-map"):
        vector = body_layout.get("vector") or body_layout.get("Vector")
        if not isinstance(vector, dict):
            failures.append(f"{path}: native {kind} layout missing vector geometry")
        else:
            require_rect(path, get_manifest_value(vector, "target"), f"native {kind} vector target", failures)
            require_positive_number(path, get_manifest_value(vector, "sourceWidth"), f"native {kind} source width", failures)
            require_positive_number(path, get_manifest_value(vector, "sourceHeight"), f"native {kind} source height", failures)
    elif kind == "flags":
        require_non_empty_list(path, body_layout, "flagCells", failures)


def require_windows_native_comparison_evidence(path: str, values: dict[str, object], failures: list[str]) -> None:
    body_kind = values.get("bodyKind")
    if not isinstance(body_kind, str) or not body_kind:
        failures.append(f"{path}: Windows comparison evidence missing bodyKind")

    content_bounds = values.get("contentBounds")
    require_rect(path, content_bounds, "Windows content bounds", failures)
    if isinstance(content_bounds, dict) and content_bounds.get("aspectRatio") in (None, ""):
        failures.append(f"{path}: Windows content bounds missing aspectRatio")

    for field in ("rowCount", "metricCount", "flagCount", "trackMapMarkerCount"):
        if not isinstance(values.get(field), int):
            failures.append(f"{path}: Windows comparison evidence missing integer {field}")
    require_positive_number(path, values.get("bytes"), "Windows screenshot byte size", failures)

    model_evidence = values.get("modelEvidence")
    if not isinstance(model_evidence, dict):
        failures.append(f"{path}: Windows comparison evidence missing modelEvidence")
        return

    if model_evidence.get("bodyKind") != body_kind:
        failures.append(
            f"{path}: Windows bodyKind {body_kind!r} does not match "
            f"modelEvidence bodyKind {model_evidence.get('bodyKind')!r}"
        )


def require_rows_with_cells(path: str, rows: object, label: str, failures: list[str]) -> None:
    if not isinstance(rows, list) or not rows:
        failures.append(f"{path}: {label} missing rows")
        return

    for index, row in enumerate(rows[:6]):
        if not isinstance(row, dict):
            continue
        if get_manifest_value(row, "kind") == "class-header":
            continue
        cells = get_manifest_value(row, "cells")
        if isinstance(cells, list) and cells:
            return
    failures.append(f"{path}: {label} missing cell bounds/text evidence")


def require_rendered_cell_evidence(path: str, rows: object, failures: list[str]) -> None:
    if not isinstance(rows, list):
        return

    saw_rendered_cells = False
    for row_index, row in enumerate(rows[:12]):
        if not isinstance(row, dict) or get_manifest_value(row, "kind") == "class-header":
            continue
        rendered_cells = get_manifest_value(row, "renderedCells")
        if rendered_cells is None:
            continue
        if not isinstance(rendered_cells, list) or not rendered_cells:
            failures.append(f"{path}: model row {row_index} renderedCells is empty")
            continue
        saw_rendered_cells = True
        saw_text = False
        for cell_index, cell in enumerate(rendered_cells[:8]):
            if not isinstance(cell, dict):
                continue
            if cell.get("text") not in (None, "") or cell.get("value") not in (None, ""):
                saw_text = True
            require_rect(path, cell.get("bounds"), f"model row {row_index} rendered cell {cell_index} bounds", failures)
        if saw_text:
            return

    if saw_rendered_cells:
        failures.append(f"{path}: renderedCells did not include any text/value in the sampled rows")


def require_metric_text_evidence(path: str, metrics: object, failures: list[str]) -> None:
    if metrics is None:
        return
    if not isinstance(metrics, list):
        failures.append(f"{path}: metric evidence is not a list")
        return

    for index, metric in enumerate(metrics[:12]):
        if not isinstance(metric, dict):
            continue
        if get_manifest_value(metric, "label") in (None, ""):
            failures.append(f"{path}: metric evidence row {index} missing label")
        if get_manifest_value(metric, "value") in (None, ""):
            failures.append(f"{path}: metric evidence row {index} missing value")
        require_rect(path, get_manifest_value(metric, "bounds"), f"metric evidence row {index} bounds", failures)
        segments = get_manifest_value(metric, "segments")
        if isinstance(segments, list):
            for segment_index, segment in enumerate(segments[:8]):
                if not isinstance(segment, dict):
                    continue
                if get_manifest_value(segment, "label") in (None, ""):
                    failures.append(f"{path}: metric evidence row {index} segment {segment_index} missing label")
                if get_manifest_value(segment, "value") in (None, ""):
                    failures.append(f"{path}: metric evidence row {index} segment {segment_index} missing value")
                require_rect(path, get_manifest_value(segment, "bounds"), f"metric evidence row {index} segment {segment_index} bounds", failures)


def require_metric_section_text_evidence(path: str, sections: object, failures: list[str]) -> None:
    if sections is None:
        return
    if not isinstance(sections, list):
        failures.append(f"{path}: metric section evidence is not a list")
        return

    for section_index, section in enumerate(sections[:8]):
        if not isinstance(section, dict):
            continue
        if get_manifest_value(section, "title") in (None, ""):
            failures.append(f"{path}: metric section {section_index} missing title")
        if "bounds" in section or "Bounds" in section:
            require_rect(path, get_manifest_value(section, "bounds"), f"metric section {section_index} bounds", failures)
        require_metric_text_evidence(path, get_manifest_value(section, "rows"), failures)


def require_grid_section_text_evidence(path: str, sections: object, failures: list[str]) -> None:
    if sections is None:
        return
    if not isinstance(sections, list):
        failures.append(f"{path}: grid section evidence is not a list")
        return

    for section_index, section in enumerate(sections[:6]):
        if not isinstance(section, dict):
            continue
        if get_manifest_value(section, "title") in (None, ""):
            failures.append(f"{path}: grid section {section_index} missing title")
        require_rect(path, get_manifest_value(section, "bounds"), f"grid section {section_index} bounds", failures)
        rows = get_manifest_value(section, "rows")
        if not isinstance(rows, list):
            continue
        for row_index, row in enumerate(rows[:8]):
            if not isinstance(row, dict):
                continue
            if get_manifest_value(row, "label") in (None, ""):
                failures.append(f"{path}: grid section {section_index} row {row_index} missing label")
            require_rect(path, get_manifest_value(row, "bounds"), f"grid section {section_index} row {row_index} bounds", failures)
            cells = get_manifest_value(row, "cells")
            if isinstance(cells, list) and not cells:
                failures.append(f"{path}: grid section {section_index} row {row_index} has no cells")
            if isinstance(cells, list):
                for cell_index, cell in enumerate(cells[:8]):
                    if not isinstance(cell, dict):
                        continue
                    require_rect(path, get_manifest_value(cell, "bounds"), f"grid section {section_index} row {row_index} cell {cell_index} bounds", failures)


def require_vector_item_evidence(path: str, items: object, failures: list[str]) -> None:
    if not isinstance(items, list):
        return

    for index, item in enumerate(items[:24]):
        if not isinstance(item, dict):
            continue
        if item.get("kind") in (None, ""):
            failures.append(f"{path}: vector item {index} missing kind")
        require_rect(path, item.get("bounds"), f"vector item {index} bounds", failures)
        if "fill" in item and item.get("fill") in (None, ""):
            failures.append(f"{path}: vector item {index} missing fill color")
        if "stroke" in item and item.get("stroke") in (None, ""):
            failures.append(f"{path}: vector item {index} missing stroke color")


def require_vector_primitive_evidence(path: str, primitives: object, failures: list[str]) -> None:
    if primitives is None:
        return
    if not isinstance(primitives, list):
        failures.append(f"{path}: vector primitive evidence is not a list")
        return

    for index, primitive in enumerate(primitives[:40]):
        if not isinstance(primitive, dict):
            continue
        if primitive.get("kind") in (None, ""):
            failures.append(f"{path}: vector primitive {index} missing kind")
        points = primitive.get("points")
        bounds = primitive.get("bounds")
        if isinstance(points, list) and points:
            for point_index, point in enumerate(points[:8]):
                if not isinstance(point, dict):
                    continue
                for key in ("x", "y"):
                    if not isinstance(point.get(key), (int, float)):
                        failures.append(f"{path}: vector primitive {index} point {point_index} missing numeric {key}")
        elif bounds is not None:
            require_rect(path, bounds, f"vector primitive {index} bounds", failures)
        else:
            failures.append(f"{path}: vector primitive {index} missing points/bounds")


def require_vector_label_evidence(path: str, labels: object, failures: list[str]) -> None:
    if labels is None:
        return
    if not isinstance(labels, list):
        failures.append(f"{path}: vector label evidence is not a list")
        return

    for index, label in enumerate(labels[:24]):
        if not isinstance(label, dict):
            continue
        if label.get("text") in (None, ""):
            failures.append(f"{path}: vector label {index} missing text")
        if label.get("color") in (None, ""):
            failures.append(f"{path}: vector label {index} missing color")
        require_rect(path, label.get("bounds"), f"vector label {index} bounds", failures)


def require_non_empty_list(path: str, values: dict[str, object], key: str, failures: list[str]) -> None:
    if not non_empty_list(get_manifest_value(values, key)):
        failures.append(f"{path}: manifest evidence missing non-empty {key}")


def require_list_key(path: str, values: dict[str, object], key: str, failures: list[str]) -> None:
    if not isinstance(get_manifest_value(values, key), list):
        failures.append(f"{path}: manifest evidence missing {key} list")


def non_empty_list(value: object) -> bool:
    return isinstance(value, list) and len(value) > 0


def get_manifest_value(values: dict[str, object], key: str) -> object:
    if key in values:
        return values[key]
    pascal = key[:1].upper() + key[1:]
    return values.get(pascal)


def require_rect(path: str, value: object, label: str, failures: list[str]) -> None:
    if not isinstance(value, dict):
        failures.append(f"{path}: {label} missing rectangle")
        return

    for key in ("x", "y", "width", "height"):
        if key not in value and key[:1].upper() + key[1:] not in value:
            failures.append(f"{path}: {label} missing {key}")
            continue
        actual = get_manifest_value(value, key)
        if not isinstance(actual, (int, float)):
            failures.append(f"{path}: {label} {key} must be numeric")


def require_positive_number(path: str, value: object, label: str, failures: list[str]) -> None:
    if not isinstance(value, (int, float)) or value <= 0:
        failures.append(f"{path}: {label} must be positive, got {value!r}")


def require_non_negative_int(path: str, value: object, label: str, failures: list[str]) -> None:
    if not isinstance(value, int) or value < 0:
        failures.append(f"{path}: {label} must be a non-negative integer, got {value!r}")


def validate_localhost_alias_manifest(path: str, values: dict[str, object], failures: list[str]) -> None:
    expected_alias = expected_localhost_alias_route(path)
    if expected_alias is None:
        return

    require_manifest_fields(path, values, ["routeAlias"], failures)
    actual_alias = values.get("routeAlias")
    if actual_alias != expected_alias:
        failures.append(f"{path}: expected routeAlias {expected_alias!r}, got {actual_alias!r}")


def expected_localhost_alias_route(path: str) -> str | None:
    for overlay_id, aliases in LOCALHOST_OVERLAY_ALIASES.items():
        for alias_slug, alias_route in aliases:
            stem = f"localhost-overlays/{overlay_id}-alias-{alias_slug}"
            if path == f"{stem}.png":
                return alias_route
            for mode in preview_modes_for_overlay(overlay_id):
                if path == f"{stem}-{mode}.png":
                    return alias_route
    return None


def validate_settings_region_manifest(path: str, values: dict[str, object], failures: list[str]) -> None:
    tab = values.get("tab")
    if tab in (None, "general", "support"):
        return

    expected_region = normalize_manifest_region(values.get("region"))
    actual_region = normalize_manifest_region(values.get("activeRegion"))
    if actual_region != expected_region:
        failures.append(
            f"{path}: expected activeRegion {expected_region!r}, got {actual_region!r}; "
            "settings screenshot may have rendered the wrong region"
        )


def normalize_manifest_region(value: object) -> str:
    return str(value or "").strip().lower()


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

    if overlay_id == "car-radar":
        radar_should_render = values.get("radarShouldRender")
        if radar_should_render is False:
            failures.append(f"{path}: car radar preview model reported radarShouldRender=false")
        if path.startswith("native-overlays/"):
            if radar_should_render is not True:
                failures.append(f"{path}: native car radar manifest did not prove radarShouldRender=true")
            surface_alpha = values.get("radarSurfaceAlpha")
            if not isinstance(surface_alpha, (int, float)) or surface_alpha <= 0.1:
                failures.append(f"{path}: native car radar surface alpha {surface_alpha!r} is too low for screenshot validation")

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
