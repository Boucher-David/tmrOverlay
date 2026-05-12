#!/usr/bin/env python3
"""Export sampled raw-capture frames as browser Standings replay models."""

from __future__ import annotations

import argparse
import json
import math
import re
import struct
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from extract_live_telemetry_fixture_corpus import (  # noqa: E402
    ARRAY_FIELDS,
    CAPTURE_HEADER_BYTES,
    FRAME_HEADER,
    SCALAR_FIELDS,
    all_timing_cars,
    classify_session_kind,
    current_session,
    first_number,
    load_schema,
    load_session_snapshots,
    normalize_color,
    parse_frame_header,
    selected_scoring_rows,
    session_info_for_update,
    session_phase,
    source_category,
    unpack_array,
    unpack_value,
    valid_lap_time,
    valid_non_negative,
)


DEFAULT_COLUMNS = [
    {"id": "standings.class-position", "label": "CLS", "dataKey": "class-position", "width": 35, "alignment": "right"},
    {"id": "standings.car-number", "label": "CAR", "dataKey": "car-number", "width": 50, "alignment": "right"},
    {"id": "standings.driver", "label": "Driver", "dataKey": "driver", "width": 250, "alignment": "left"},
    {"id": "standings.gap", "label": "GAP", "dataKey": "gap", "width": 60, "alignment": "right"},
    {"id": "standings.interval", "label": "INT", "dataKey": "interval", "width": 60, "alignment": "right"},
    {"id": "standings.pit", "label": "PIT", "dataKey": "pit", "width": 30, "alignment": "right"},
]

LIKELY_CLASS_TOKENS = {
    "GT3",
    "GT4",
    "GTP",
    "TCR",
    "LMP2",
    "LMP3",
    "MX5",
    "GR86",
    "PCUP",
}
ON_TRACK_SURFACE = 3


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, document: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, indent=2), encoding="utf-8")


def finite(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))


def signed_seconds(value: float | None, digits: int = 1) -> str:
    if value is None or not finite(value):
        return "--"
    return f"{'+' if value > 0 else ''}{value:.{digits}f}"


def valid_timing_seconds(value: Any) -> float | None:
    seconds = valid_non_negative(value)
    return seconds if seconds is not None and seconds > 0.05 else None


def car_number(value: Any, car_idx: int) -> str:
    text = str(value or "").strip().lstrip("#")
    return f"#{text}" if text else f"#{car_idx}"


def driver_directory(session_data: dict[str, Any]) -> dict[int, dict[str, Any]]:
    source_drivers = [
        driver
        for driver in ((session_data.get("DriverInfo") or {}).get("Drivers") or [])
        if isinstance(driver.get("CarIdx"), int) and 0 <= driver["CarIdx"] < 64
    ]
    class_names = class_names_by_id(source_drivers)
    drivers: dict[int, dict[str, Any]] = {}
    for driver in source_drivers:
        car_idx = driver.get("CarIdx")
        car_class_id = driver.get("CarClassID")
        drivers[car_idx] = {
            "carIdx": car_idx,
            "driverName": str(driver.get("UserName") or "").strip(),
            "teamName": str(driver.get("TeamName") or "").strip(),
            "carNumber": str(driver.get("CarNumber") or "").strip(),
            "carClassId": car_class_id,
            "carClassName": class_names.get(car_class_id) or str(driver.get("CarClassShortName") or "").strip(),
            "carClassColorHex": normalize_color(driver.get("CarClassColor")),
            "carScreenName": str(driver.get("CarScreenNameShort") or driver.get("CarScreenName") or "").strip(),
        }
    return drivers


def class_names_by_id(source_drivers: list[dict[str, Any]]) -> dict[Any, str]:
    grouped: dict[Any, list[dict[str, Any]]] = defaultdict(list)
    for driver in source_drivers:
        grouped[driver.get("CarClassID")].append(driver)

    names: dict[Any, str] = {}
    for car_class_id, drivers in grouped.items():
        explicit = first_non_empty(str(driver.get("CarClassShortName") or "").strip() for driver in drivers)
        if explicit:
            names[car_class_id] = explicit
            continue

        car_names = [
            str(driver.get("CarScreenNameShort") or driver.get("CarScreenName") or "").strip()
            for driver in drivers
            if str(driver.get("CarScreenNameShort") or driver.get("CarScreenName") or "").strip()
        ]
        common_token = common_likely_class_token(car_names)
        if common_token:
            names[car_class_id] = common_token
            continue

        unique_names = sorted(set(car_names))
        if len(unique_names) == 1:
            names[car_class_id] = unique_names[0]
        elif car_class_id is not None:
            names[car_class_id] = f"Class {car_class_id}"
        else:
            names[car_class_id] = "Standings"
    return names


def first_non_empty(values: Iterable[str]) -> str | None:
    return next((value for value in values if value), None)


def common_likely_class_token(car_names: list[str]) -> str | None:
    if not car_names:
        return None

    token_sets = [set(re.findall(r"[A-Z0-9]+", name.upper().replace("-", " "))) for name in car_names]
    common = set.intersection(*token_sets) if token_sets else set()
    candidates = [token for token in common if likely_class_token(token)]
    return sorted(candidates, key=lambda token: (token not in LIKELY_CLASS_TOKENS, len(token), token))[0] if candidates else None


def likely_class_token(token: str) -> bool:
    return token in LIKELY_CLASS_TOKENS or any(character.isdigit() for character in token)


def driver_name(driver: dict[str, Any] | None, car_idx: int) -> str:
    if driver:
        for key in ("driverName", "teamName"):
            value = str(driver.get(key) or "").strip()
            if value:
                return value
    return f"Car {car_idx}"


def class_label(driver: dict[str, Any] | None, car_class: Any) -> str:
    if driver:
        value = str(driver.get("carClassName") or "").strip()
        if value:
            return value
        car = str(driver.get("carScreenName") or "").strip()
        if car:
            return car
    return f"Class {car_class}" if car_class is not None else "Standings"


def class_color(driver: dict[str, Any] | None) -> str | None:
    if not driver or not driver.get("carClassColorHex"):
        return None
    text = str(driver["carClassColorHex"]).strip().lstrip("#")
    if len(text) == 8:
        text = text[-6:]
    return f"#{text}" if len(text) == 6 else None


def frame_indexes(frame_count: int, max_frames: int, stride: int | None, start_frame: int) -> list[int]:
    if frame_count <= 0:
        return []
    start = min(max(1, start_frame), frame_count)
    if stride is None:
        stride = max(1, math.ceil((frame_count - start + 1) / max(1, max_frames)))
    values = list(range(start, frame_count + 1, max(1, stride)))
    return values[: max(1, max_frames)]


def read_capture_frame(
    telemetry_path: Path,
    buffer_length: int,
    requested_index: int,
) -> tuple[dict[str, Any], bytes] | None:
    record_bytes = FRAME_HEADER.size + buffer_length
    with telemetry_path.open("rb") as handle:
        handle.seek(CAPTURE_HEADER_BYTES + (requested_index - 1) * record_bytes)
        header = parse_frame_header(handle.read(FRAME_HEADER.size))
        if header is None:
            return None
        payload_length = int(header["payloadLength"])
        payload = handle.read(payload_length)
        if len(payload) != payload_length:
            return None
        return header, payload


def extract_raw(schema: dict[str, dict[str, Any]], payload: bytes) -> tuple[dict[str, Any], dict[str, list[Any]]]:
    return (
        {name: unpack_value(payload, schema.get(name)) for name in SCALAR_FIELDS},
        {name: unpack_array(payload, schema.get(name)) for name in ARRAY_FIELDS},
    )


def timing_lookup(values: dict[str, list[Any]], gridded_car_idxs: set[int] | None = None) -> dict[int, dict[str, Any]]:
    rows: dict[int, dict[str, Any]] = {}
    for car in all_timing_cars(values):
        rows[car.car_idx] = {
            "carIdx": car.car_idx,
            "overallPosition": car.position,
            "classPosition": car.class_position,
            "carClass": car.car_class,
            "f2TimeSeconds": car.f2_time,
            "estimatedTimeSeconds": car.estimated_time,
            "lastLapTimeSeconds": valid_lap_time(car.last_lap_time),
            "bestLapTimeSeconds": valid_lap_time(car.best_lap_time),
            "trackSurface": car.track_surface,
            "onPitRoad": car.on_pit_road,
            "hasTakenGrid": has_taken_grid(car.car_idx, car.track_surface, car.on_pit_road, gridded_car_idxs),
        }
    return rows


def reference_car_idx(raw: dict[str, Any], timing_rows: dict[int, dict[str, Any]]) -> int | None:
    raw_cam = raw.get("CamCarIdx")
    if isinstance(raw_cam, int) and 0 <= raw_cam < 64 and raw_cam in timing_rows:
        return raw_cam
    return None


def row_has_valid_lap(row: dict[str, Any]) -> bool:
    return valid_lap_time(row.get("bestLapTimeSeconds")) is not None or valid_lap_time(row.get("lastLapTimeSeconds")) is not None


def update_gridded_cars(
    raw: dict[str, Any],
    values: dict[str, list[Any]],
    session_data: dict[str, Any],
    gridded_car_idxs: set[int],
) -> None:
    selected = current_session(session_data)
    session_kind = classify_session_kind(str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or ""))
    session_state = raw.get("SessionState")
    if session_kind != "race" or not isinstance(session_state, int) or session_state >= 4:
        return

    for car in all_timing_cars(values):
        if car.track_surface == ON_TRACK_SURFACE and car.on_pit_road is not True:
            gridded_car_idxs.add(car.car_idx)


def has_taken_grid(
    car_idx: int,
    track_surface: int | None,
    on_pit_road: bool | None,
    gridded_car_idxs: set[int] | None,
) -> bool:
    return (
        (gridded_car_idxs is not None and car_idx in gridded_car_idxs)
        or (track_surface == ON_TRACK_SURFACE and on_pit_road is not True)
    )


def row_car_class(row: dict[str, Any], timing: dict[str, Any] | None, drivers: dict[int, dict[str, Any]]) -> Any:
    car_idx = row["carIdx"]
    driver = drivers.get(car_idx)
    return (
        row.get("carClass")
        or (timing or {}).get("carClass")
        or (driver or {}).get("carClassId")
        or "unknown"
    )


def scoring_display_rows(
    raw: dict[str, Any],
    session_data: dict[str, Any],
    values: dict[str, list[Any]],
    other_class_rows: int,
    maximum_rows: int,
    gridded_car_idxs: set[int],
) -> tuple[str, str, list[dict[str, Any]]]:
    selected = current_session(session_data)
    session_kind = classify_session_kind(str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or ""))
    selected_source, scoring_rows = selected_scoring_rows(session_data, raw, selected)
    timing_rows = timing_lookup(values, gridded_car_idxs)
    ref_idx = reference_car_idx(raw, timing_rows)
    drivers = driver_directory(session_data)
    requires_valid_lap = session_kind in ("practice", "qualifying", "test")
    if requires_valid_lap:
        scoring_rows = [row for row in scoring_rows if row_has_valid_lap(row)]
    if not scoring_rows:
        return "waiting for standings", "source: waiting", []

    rows_by_class: dict[Any, list[dict[str, Any]]] = defaultdict(list)
    for row in scoring_rows:
        timing = timing_rows.get(row["carIdx"])
        rows_by_class[row_car_class(row, timing, drivers)].append(row)
    groups = sorted(rows_by_class.items(), key=lambda item: min(row.get("overallPosition") or 999 for row in item[1]))
    primary_key = next((key for key, rows in groups if ref_idx is not None and any(row["carIdx"] == ref_idx for row in rows)), groups[0][0])
    visible_other = max(0, min(6, other_class_rows))
    include_other = visible_other > 0 and len(groups) > 1
    include_headers = include_other
    row_budget = maximum_rows if not include_other else min(64, maximum_rows + len(groups) + (len(groups) - 1) * (1 + visible_other))
    reserved_other = (len(groups) - 1) * (1 + visible_other) if include_other else 0
    primary_limit = max(2 if include_headers else 1, row_budget - reserved_other)

    display_rows: list[dict[str, Any]] = []
    for key, rows in groups:
        is_primary = key == primary_key
        if not is_primary and not include_other:
            continue
        group_limit = primary_limit if is_primary else visible_other
        if include_headers:
            first_driver = drivers.get(rows[0]["carIdx"])
            display_rows.append(header_row(class_label(first_driver, key), f"{len(rows)} cars", class_color(first_driver)))
        for row in select_around_reference(rows, ref_idx if is_primary else None, group_limit):
            timing = timing_rows.get(row["carIdx"])
            display_rows.append(
                car_display_row(
                    row,
                    timing,
                    ref_idx,
                    rows,
                    timing_rows,
                    drivers,
                    is_pending_grid=(
                        selected_source.startswith("starting-grid")
                        and isinstance(raw.get("SessionState"), int)
                        and 0 < raw["SessionState"] < 4
                        and not has_taken_grid(
                            row["carIdx"],
                            (timing or {}).get("trackSurface"),
                            (timing or {}).get("onPitRoad"),
                            gridded_car_idxs,
                        )
                    ),
                )
            )
            if len(display_rows) >= row_budget:
                break
        if len(display_rows) >= row_budget:
            break

    car_count = sum(1 for row in display_rows if not row.get("isClassHeader"))
    reference = next((row for row in display_rows if row.get("isReference")), None)
    status_prefix = reference["cells"][0] if reference and reference.get("cells") else None
    status = f"{status_prefix} - {car_count}/{len(scoring_rows)} rows" if status_prefix and status_prefix != "--" else f"{car_count}/{len(scoring_rows)} rows"
    source = "source: starting grid" if selected_source.startswith("starting-grid") else "source: scoring snapshot + live timing"
    return status, source, display_rows


def timing_display_rows(
    raw: dict[str, Any],
    session_data: dict[str, Any],
    values: dict[str, list[Any]],
    maximum_rows: int,
) -> tuple[str, str, list[dict[str, Any]]]:
    timing_rows = timing_lookup(values)
    ref_idx = reference_car_idx(raw, timing_rows)
    if ref_idx is None:
        return "waiting for focus car", "source: waiting", []
    drivers = driver_directory(session_data)
    rows = sorted(
        timing_rows.values(),
        key=lambda row: (
            row.get("classPosition") or 999,
            row.get("overallPosition") or 999,
            row.get("carIdx") or 999,
        ),
    )
    rows = select_around_reference(rows, ref_idx, maximum_rows)
    display_rows = [car_display_row(timing, timing, ref_idx, rows, timing_rows, drivers) for timing in rows]
    reference = next((row for row in display_rows if row.get("isReference")), None)
    status_prefix = reference["cells"][0] if reference and reference.get("cells") else None
    status = f"{status_prefix} - {len(display_rows)} rows" if status_prefix and status_prefix != "--" else f"{len(display_rows)} rows"
    return status, "source: live timing telemetry", display_rows


def select_around_reference(rows: list[dict[str, Any]], ref_idx: int | None, limit: int) -> list[dict[str, Any]]:
    ordered = sorted(rows, key=lambda row: (row.get("classPosition") or 999, row.get("overallPosition") or 999, row["carIdx"]))
    if limit <= 0 or len(ordered) <= limit:
        return ordered[: max(0, limit)]
    if ref_idx is None:
        return ordered[:limit]
    ref_pos = next((index for index, row in enumerate(ordered) if row["carIdx"] == ref_idx), -1)
    if ref_pos < 0:
        return ordered[:limit]
    ahead = limit // 2
    start = max(0, min(ref_pos - ahead, len(ordered) - limit))
    return ordered[start : start + limit]


def car_display_row(
    row: dict[str, Any],
    timing: dict[str, Any] | None,
    ref_idx: int | None,
    group_rows: list[dict[str, Any]],
    timing_rows: dict[int, dict[str, Any]],
    drivers: dict[int, dict[str, Any]],
    is_pending_grid: bool = False,
) -> dict[str, Any]:
    car_idx = row["carIdx"]
    timing = timing or {}
    driver = drivers.get(car_idx)
    ref_timing = timing_rows.get(ref_idx) if ref_idx is not None else None
    class_position = row.get("classPosition") or timing.get("classPosition")
    f2 = valid_timing_seconds(timing.get("f2TimeSeconds"))
    leader_f2 = class_leader_f2(row, group_rows, timing_rows)
    gap = "Leader" if class_position == 1 else signed_seconds(f2 - leader_f2 if f2 is not None and leader_f2 is not None else None)
    ref_f2 = valid_timing_seconds((ref_timing or {}).get("f2TimeSeconds"))
    interval = "0.0" if ref_idx == car_idx else signed_seconds(f2 - ref_f2 if f2 is not None and ref_f2 is not None else None)
    return {
        "cells": [
            str(class_position) if class_position else "--",
            car_number((driver or {}).get("carNumber"), car_idx),
            driver_name(driver, car_idx),
            gap,
            interval,
            "IN" if timing.get("onPitRoad") is True else "",
        ],
        "isClassHeader": False,
        "isReference": ref_idx == car_idx,
        "isPit": timing.get("onPitRoad") is True,
        "isPartial": not timing,
        "isPendingGrid": is_pending_grid,
        "carClassColorHex": class_color(driver),
        "headerTitle": None,
        "headerDetail": None,
    }


def class_leader_f2(row: dict[str, Any], group_rows: Iterable[dict[str, Any]], timing_rows: dict[int, dict[str, Any]]) -> float | None:
    leader = next((candidate for candidate in group_rows if candidate.get("classPosition") == 1), None)
    if leader is not None:
        return valid_timing_seconds((timing_rows.get(leader["carIdx"]) or {}).get("f2TimeSeconds"))
    values = [
        valid_timing_seconds((timing_rows.get(candidate["carIdx"]) or {}).get("f2TimeSeconds"))
        for candidate in group_rows
    ]
    values = [value for value in values if value is not None]
    return min(values) if values else None


def header_row(title: str, detail: str, color: str | None) -> dict[str, Any]:
    return {
        "cells": [],
        "isClassHeader": True,
        "isReference": False,
        "isPit": False,
        "isPartial": False,
        "isPendingGrid": False,
        "carClassColorHex": color,
        "headerTitle": title,
        "headerDetail": detail,
    }


def format_time_remaining(raw: dict[str, Any], session_data: dict[str, Any]) -> str | None:
    remaining = valid_non_negative(raw.get("SessionTimeRemain"))
    if remaining is None:
        return None

    selected = current_session(session_data)
    session_kind = classify_session_kind(str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or ""))
    session_state = raw.get("SessionState")
    if session_kind == "race" and isinstance(session_state, int) and 1 <= session_state <= 3:
        total_seconds = int(math.ceil(max(0.0, remaining)))
        return f"{total_seconds // 60:02d}:{total_seconds % 60:02d}"

    total_minutes = int(math.ceil(max(0.0, remaining) / 60.0))
    return f"{total_minutes // 60:02d}:{total_minutes % 60:02d}"


def header_items(raw: dict[str, Any], session_data: dict[str, Any], status: str) -> list[dict[str, str]]:
    items = [{"key": "status", "value": status}]
    time_remaining = format_time_remaining(raw, session_data)
    if time_remaining:
        items.append({"key": "timeRemaining", "value": time_remaining})
    return items


def build_frame(
    capture_dir: Path,
    manifest: dict[str, Any],
    schema: dict[str, dict[str, Any]],
    header: dict[str, Any],
    payload: bytes,
    session_data: dict[str, Any],
    other_class_rows: int,
    maximum_rows: int,
    gridded_car_idxs: set[int],
) -> dict[str, Any]:
    raw, values = extract_raw(schema, payload)
    update_gridded_cars(raw, values, session_data, gridded_car_idxs)
    status, source, rows = scoring_display_rows(raw, session_data, values, other_class_rows, maximum_rows, gridded_car_idxs)
    if not rows:
        status, source, rows = timing_display_rows(raw, session_data, values, maximum_rows)
    phase = session_phase(raw.get("SessionState"))
    headers = header_items(raw, session_data, status)
    model = {
        "overlayId": "standings",
        "title": "Standings",
        "status": status,
        "source": source,
        "bodyKind": "table",
        "columns": DEFAULT_COLUMNS,
        "rows": rows,
        "metrics": [],
        "points": [],
        "headerItems": headers,
    }
    return {
        "captureId": str(manifest.get("captureId") or capture_dir.name),
        "frameIndex": header["frameIndex"],
        "capturedUnixMs": header["capturedUnixMs"],
        "sessionTimeSeconds": round(float(header["sessionTime"]), 3),
        "sessionInfoUpdate": header["sessionInfoUpdate"],
        "sessionState": raw.get("SessionState"),
        "sessionPhase": phase,
        "camCarIdx": raw.get("CamCarIdx"),
        "playerCarIdx": raw.get("PlayerCarIdx"),
        "model": model,
    }


def export_capture(args: argparse.Namespace) -> dict[str, Any]:
    capture_dir = args.capture
    manifest = read_json(capture_dir / "capture-manifest.json")
    schema = load_schema(capture_dir)
    updates, snapshots, latest = load_session_snapshots(capture_dir)
    frame_count = int(manifest.get("frameCount") or 0)
    buffer_length = int(manifest.get("bufferLength") or 0)
    telemetry_path = capture_dir / str(manifest.get("telemetryFile") or "telemetry.bin")
    frames = []
    gridded_car_idxs: set[int] = set()
    for requested_index in frame_indexes(frame_count, args.max_frames, args.stride, args.start_frame):
        frame = read_capture_frame(telemetry_path, buffer_length, requested_index)
        if frame is None:
            continue
        header, payload = frame
        session_data = session_info_for_update(header["sessionInfoUpdate"], updates, snapshots, latest)
        frames.append(build_frame(
            capture_dir,
            manifest,
            schema,
            header,
            payload,
            session_data,
            args.other_class_rows,
            args.maximum_rows,
            gridded_car_idxs))
    return {
        "schemaVersion": 1,
        "kind": "standings-browser-replay",
        "source": {
            "captureId": str(manifest.get("captureId") or capture_dir.name),
            "sourceCategory": source_category(str(manifest.get("captureId") or capture_dir.name)),
            "captureDirectory": str(capture_dir),
            "frameCount": frame_count,
            "sampledFrameCount": len(frames),
            "startedAtUtc": manifest.get("startedAtUtc"),
            "finishedAtUtc": manifest.get("finishedAtUtc"),
        },
        "frames": frames,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", type=Path, required=True, help="Raw capture directory.")
    parser.add_argument("--output", type=Path, required=True, help="Replay JSON output path.")
    parser.add_argument("--max-frames", type=int, default=720, help="Maximum sampled browser frames.")
    parser.add_argument("--stride", type=int, default=None, help="Optional 1-based raw frame stride.")
    parser.add_argument("--start-frame", type=int, default=1, help="1-based raw frame to start from.")
    parser.add_argument("--maximum-rows", type=int, default=14, help="Primary standings row budget.")
    parser.add_argument("--other-class-rows", type=int, default=2, help="Other-class rows per class.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    replay = export_capture(args)
    write_json(args.output, replay)
    print(f"Wrote {len(replay['frames'])} standings replay frames to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
