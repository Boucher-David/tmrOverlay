#!/usr/bin/env python3
"""Export sampled raw-capture frames as browser Standings replay models."""

from __future__ import annotations

import argparse
import json
import math
import re
import statistics
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
    array_value,
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

REPLAY_SCALAR_FIELDS = list(
    dict.fromkeys(
        [
            *SCALAR_FIELDS,
            "Throttle",
            "Brake",
            "Clutch",
            "ClutchRaw",
            "SteeringWheelAngle",
            "BrakeABSactive",
            "Gear",
            "RPM",
            "EngineWarnings",
            "PlayerTireCompound",
            "FuelLevel",
            "FuelLevelPct",
            "FuelUsePerHour",
            "Voltage",
            "WaterTemp",
            "FuelPress",
            "OilTemp",
            "OilPress",
            "AirTemp",
            "TrackTempCrew",
            "TrackTemp",
            "TrackWetness",
            "WeatherDeclaredWet",
            "Skies",
            "Precipitation",
            "WindVel",
            "WindDir",
            "RelativeHumidity",
            "FogLevel",
            "PlayerCarPitSvStatus",
            "TireSetsUsed",
            "FastRepairUsed",
        ]
    )
)

REPLAY_ARRAY_FIELDS = list(dict.fromkeys([*ARRAY_FIELDS, "CarIdxFastRepairsUsed"]))

ON_TRACK_SURFACE = 3
GAP_TO_LEADER_MISSING_SEGMENT_THRESHOLD_SECONDS = 10.0


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path: Path, document: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, indent=2), encoding="utf-8")


def finite(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(float(value))


def compact_number(value: Any, digits: int = 3) -> float | int | None:
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    if not finite(value):
        return None
    rounded = round(float(value), digits)
    return 0.0 if rounded == 0 else rounded


def signed_seconds(value: float | None, digits: int = 1) -> str:
    if value is None or not finite(value):
        return "--"
    return f"{'+' if value > 0 else ''}{value:.{digits}f}"


def signed_gap(seconds: float | None, laps: float | None, digits: int = 1) -> str:
    if laps is not None and finite(laps):
        positive_laps = max(0.0, float(laps))
        if positive_laps >= 0.9999:
            return f"+{positive_laps:.0f}L" if abs(positive_laps - round(positive_laps)) <= 0.0001 else f"+{positive_laps:.3f}L"
    return signed_seconds(seconds, digits)


def valid_timing_seconds(value: Any) -> float | None:
    seconds = valid_non_negative(value)
    return seconds if seconds is not None and seconds > 0.05 else None


def valid_lap_dist_pct(value: Any) -> float | None:
    if not finite(value):
        return None
    number = float(value)
    return max(0.0, min(1.0, number)) if number >= 0.0 else None


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

        if car_class_id is not None:
            names[car_class_id] = f"Class {car_class_id}"
        else:
            names[car_class_id] = "Standings"
    return names


def first_non_empty(values: Iterable[str]) -> str | None:
    return next((value for value in values if value), None)


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
    return f"Class {car_class}" if car_class is not None else "Standings"


def class_color(driver: dict[str, Any] | None) -> str | None:
    if not driver or not driver.get("carClassColorHex"):
        return None
    text = str(driver["carClassColorHex"]).strip().lstrip("#")
    if len(text) == 8:
        text = text[-6:]
    return f"#{text}" if len(text) == 6 else None


def track_wetness_label(value: Any) -> str | None:
    if not isinstance(value, int):
        return None
    if value <= 1:
        return "dry"
    if value <= 3:
        return "damp"
    return "wet"


def skies_label(value: Any, session_data: dict[str, Any]) -> str | None:
    if isinstance(value, int):
        return {
            0: "clear",
            1: "partly cloudy",
            2: "mostly cloudy",
            3: "overcast",
        }.get(value, f"skies {value}")
    weekend_skies = str(((session_data.get("WeekendInfo") or {}).get("TrackSkies") or "")).strip()
    return weekend_skies or None


def frame_indexes(frame_count: int, max_frames: int, stride: int | None, start_frame: int) -> list[int]:
    if frame_count <= 0:
        return []
    start = min(max(1, start_frame), frame_count)
    if stride is None:
        stride = max(1, math.ceil((frame_count - start + 1) / max(1, max_frames)))
    values = list(range(start, frame_count + 1, max(1, stride)))
    return values[: max(1, max_frames)]


def readable_frame_count(telemetry_path: Path, buffer_length: int, manifest_frame_count: int) -> int:
    if buffer_length <= 0 or not telemetry_path.exists():
        return max(0, manifest_frame_count)

    record_bytes = FRAME_HEADER.size + buffer_length
    payload_bytes = max(0, telemetry_path.stat().st_size - CAPTURE_HEADER_BYTES)
    file_frame_count = payload_bytes // record_bytes
    if manifest_frame_count > 0:
        return min(manifest_frame_count, file_frame_count) if file_frame_count > 0 else manifest_frame_count
    return max(0, file_frame_count)


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
        {name: unpack_value(payload, schema.get(name)) for name in REPLAY_SCALAR_FIELDS},
        {name: unpack_array(payload, schema.get(name)) for name in REPLAY_ARRAY_FIELDS},
    )


def timing_lookup(values: dict[str, list[Any]], gridded_car_idxs: set[int] | None = None) -> dict[int, dict[str, Any]]:
    rows: dict[int, dict[str, Any]] = {}
    for car in all_timing_cars(values):
        rows[car.car_idx] = {
            "carIdx": car.car_idx,
            "overallPosition": car.position,
            "classPosition": car.class_position,
            "carClass": car.car_class,
            "lapCompleted": car.lap_completed,
            "lapDistPct": valid_lap_dist_pct(car.lap_dist_pct),
            "f2TimeSeconds": car.f2_time,
            "estimatedTimeSeconds": car.estimated_time,
            "lastLapTimeSeconds": valid_lap_time(car.last_lap_time),
            "bestLapTimeSeconds": valid_lap_time(car.best_lap_time),
            "trackSurface": car.track_surface,
            "onPitRoad": car.on_pit_road,
            "hasTakenGrid": has_taken_grid(car.car_idx, car.track_surface, car.on_pit_road, gridded_car_idxs),
        }
    return rows


def unit_interval(value: Any) -> float | None:
    if not finite(value):
        return None
    return max(0.0, min(1.0, float(value)))


def positive(value: Any) -> float | None:
    if finite(value) and float(value) > 0:
        return float(value)
    return None


def track_length_meters(session_data: dict[str, Any]) -> float | None:
    raw_length = (session_data.get("WeekendInfo") or {}).get("TrackLength")
    if finite(raw_length):
        return float(raw_length) * 1000.0
    if isinstance(raw_length, str):
        match = re.search(r"([-+]?\d+(?:\.\d+)?)", raw_length)
        if match:
            value = float(match.group(1))
            return value * (1609.344 if "mi" in raw_length.lower() else 1000.0)
    return None


def session_type_label(session_kind: str | None, selected: dict[str, Any] | None) -> str:
    if session_kind == "qualifying":
        return "Qualify"
    if session_kind:
        return session_kind.capitalize()
    return str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or "Session")


def live_driver_rows(drivers: dict[int, dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "carIdx": driver["carIdx"],
            "driverName": driver.get("driverName") or None,
            "teamName": driver.get("teamName") or None,
            "carNumber": driver.get("carNumber") or None,
            "carClass": driver.get("carClassId"),
            "carClassName": driver.get("carClassName") or None,
            "carClassColorHex": class_color(driver),
            "carScreenName": driver.get("carScreenName") or None,
        }
        for _, driver in sorted(drivers.items())
    ]


def enrich_live_row(row: dict[str, Any], driver: dict[str, Any] | None, focus_idx: int | None, player_idx: int | None) -> dict[str, Any]:
    return {
        **row,
        "driverName": driver_name(driver, row["carIdx"]),
        "teamName": (driver or {}).get("teamName") or None,
        "carNumber": (driver or {}).get("carNumber") or str(row["carIdx"]),
        "carClassName": class_label(driver, row.get("carClass")),
        "carClassColorHex": class_color(driver),
        "isFocus": row["carIdx"] == focus_idx,
        "isPlayer": row["carIdx"] == player_idx,
    }


def timing_live_rows(
    timing_rows: dict[int, dict[str, Any]],
    drivers: dict[int, dict[str, Any]],
    focus_idx: int | None,
    player_idx: int | None,
) -> list[dict[str, Any]]:
    return [
        enrich_live_row(row, drivers.get(car_idx), focus_idx, player_idx)
        for car_idx, row in sorted(
            timing_rows.items(),
            key=lambda item: (
                item[1].get("overallPosition") or 999,
                item[1].get("classPosition") or 999,
                item[0],
            ),
        )
    ]


def scoring_live_rows(
    scoring_rows: list[dict[str, Any]],
    timing_rows: dict[int, dict[str, Any]],
    drivers: dict[int, dict[str, Any]],
    focus_idx: int | None,
    player_idx: int | None,
) -> list[dict[str, Any]]:
    rows = []
    for row in scoring_rows:
        car_idx = row["carIdx"]
        timing = timing_rows.get(car_idx) or {}
        driver = drivers.get(car_idx)
        rows.append(
            enrich_live_row(
                {
                    "carIdx": car_idx,
                    "overallPosition": row.get("overallPosition") or timing.get("overallPosition"),
                    "classPosition": row.get("classPosition") or timing.get("classPosition"),
                    "carClass": row_car_class(row, timing, drivers),
                    "lastLapTimeSeconds": row.get("lastLapTimeSeconds") or timing.get("lastLapTimeSeconds"),
                    "bestLapTimeSeconds": row.get("bestLapTimeSeconds") or timing.get("bestLapTimeSeconds"),
                    "onPitRoad": timing.get("onPitRoad"),
                    "trackSurface": timing.get("trackSurface"),
                },
                driver,
                focus_idx,
                player_idx,
            )
        )
    return rows


def live_relative_rows(
    timing_rows: dict[int, dict[str, Any]],
    drivers: dict[int, dict[str, Any]],
    focus_idx: int | None,
    player_idx: int | None,
) -> list[dict[str, Any]]:
    focus = timing_rows.get(focus_idx) if focus_idx is not None else None
    if not focus:
        return []
    focus_progress = focus.get("lapCompleted")
    focus_pct = focus.get("lapDistPct")
    focus_est = focus.get("estimatedTimeSeconds")
    focus_f2 = usable_f2_for_timing(focus, True)
    if isinstance(focus_progress, int) and finite(focus_pct):
        focus_laps = focus_progress + float(focus_pct)
    else:
        focus_laps = None
    rows = []
    for car_idx, row in timing_rows.items():
        if car_idx == focus_idx:
            continue
        relative_seconds = None
        if valid_timing_seconds(row.get("estimatedTimeSeconds")) is not None and valid_timing_seconds(focus_est) is not None:
            relative_seconds = float(row["estimatedTimeSeconds"]) - float(focus_est)
        elif usable_f2_for_timing(row, True) is not None and focus_f2 is not None:
            relative_seconds = float(row["f2TimeSeconds"]) - float(focus_f2)
        relative_laps = None
        if focus_laps is not None and isinstance(row.get("lapCompleted"), int) and finite(row.get("lapDistPct")):
            relative_laps = row["lapCompleted"] + float(row["lapDistPct"]) - focus_laps
        if relative_seconds is None and relative_laps is None:
            continue
        rows.append(
            enrich_live_row(
                {
                    **row,
                    "relativeSeconds": relative_seconds,
                    "relativeLaps": relative_laps,
                    "isAhead": relative_seconds is not None and relative_seconds < 0,
                    "isBehind": relative_seconds is not None and relative_seconds > 0,
                },
                drivers.get(car_idx),
                focus_idx,
                player_idx,
            )
        )
    return sorted(rows, key=lambda row: abs(row.get("relativeSeconds") if row.get("relativeSeconds") is not None else (row.get("relativeLaps") or 99) * 120))[:16]


def live_spatial_model(
    raw: dict[str, Any],
    relative_rows: list[dict[str, Any]],
    reference: dict[str, Any] | None,
    track_length: float | None,
) -> dict[str, Any]:
    side = raw.get("CarLeftRight")
    has_left = side in (2, 4, 5)
    has_right = side in (3, 4, 6)
    cars = [
        {
            "carIdx": row["carIdx"],
            "relativeSeconds": row.get("relativeSeconds"),
            "relativeLaps": row.get("relativeLaps"),
            "relativeMeters": row.get("relativeMeters"),
            "carClassColorHex": row.get("carClassColorHex"),
        }
        for row in spatial_rows(relative_rows, track_length)
    ]
    return {
        "hasData": bool(reference) or bool(cars),
        "quality": "capture-derived",
        "referenceCarIdx": reference.get("carIdx") if reference else None,
        "referenceLapDistPct": reference.get("lapDistPct") if reference else None,
        "trackLengthMeters": track_length,
        "hasCarLeft": has_left,
        "hasCarRight": has_right,
        "sideStatus": "both" if has_left and has_right else "left" if has_left else "right" if has_right else "clear",
        "strongestMulticlassApproach": next(
            (
                {"relativeSeconds": row.get("relativeSeconds")}
                for row in relative_rows
                if row.get("relativeSeconds") is not None and float(row["relativeSeconds"]) < 0
            ),
            None,
        ),
        "cars": cars,
    }


def spatial_rows(relative_rows: list[dict[str, Any]], track_length: float | None) -> list[dict[str, Any]]:
    rows = []
    for row in relative_rows:
        relative_meters = row.get("relativeMeters")
        if relative_meters is None and finite(row.get("relativeLaps")) and positive(track_length) is not None:
            relative_meters = float(row["relativeLaps"]) * float(track_length)
        if relative_meters is None or not finite(relative_meters):
            continue
        if abs(float(relative_meters)) > 4.746 * 6:
            continue
        rows.append({**row, "relativeMeters": float(relative_meters)})
    return rows


def build_live_snapshot(
    replay_index: int,
    replay_frame: dict[str, Any],
    raw: dict[str, Any],
    session_data: dict[str, Any],
    values: dict[str, list[Any]],
    gridded_car_idxs: set[int],
) -> dict[str, Any]:
    selected = current_session(session_data)
    session_kind = classify_session_kind(str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or ""))
    session_type = session_type_label(session_kind, selected)
    timing_rows = timing_lookup(values, gridded_car_idxs)
    drivers = driver_directory(session_data)
    focus_idx = reference_car_idx(raw, timing_rows)
    player_idx = raw.get("PlayerCarIdx") if isinstance(raw.get("PlayerCarIdx"), int) and 0 <= raw.get("PlayerCarIdx") < 64 else None
    reference = timing_rows.get(focus_idx) if focus_idx is not None else None
    selected_source, selected_rows = selected_scoring_rows(session_data, raw, selected, all_timing_cars(values))
    timing_live = timing_live_rows(timing_rows, drivers, focus_idx, player_idx)
    scoring_live = scoring_live_rows(selected_rows, timing_rows, drivers, focus_idx, player_idx)
    relative_live = live_relative_rows(timing_rows, drivers, focus_idx, player_idx)
    track_length = track_length_meters(session_data)
    track_wetness = raw.get("TrackWetness") if isinstance(raw.get("TrackWetness"), int) else None
    clutch = unit_interval(raw.get("ClutchRaw")) if unit_interval(raw.get("Clutch")) in (None, 0.0) else unit_interval(raw.get("Clutch"))
    fuel_level = positive(raw.get("FuelLevel"))
    fuel_pct = unit_interval(raw.get("FuelLevelPct"))
    on_pit_road = bool(raw.get("OnPitRoad") or raw.get("PlayerCarInPitStall") or (reference or {}).get("onPitRoad"))
    session_time_remain = valid_non_negative(raw.get("SessionTimeRemain"))
    car_screen_name = str(((session_data.get("DriverInfo") or {}).get("DriverCarScreenName") or "")).strip()
    track_display_name = str(((session_data.get("WeekendInfo") or {}).get("TrackDisplayName") or "")).strip()
    return {
        "isConnected": True,
        "isCollecting": True,
        "sourceId": replay_frame["captureId"],
        "startedAtUtc": None,
        "lastUpdatedAtUtc": None,
        "sequence": replay_index + 1,
        "context": {
            "session": {
                "sessionType": session_type,
                "sessionName": str((selected or {}).get("SessionName") or session_type),
                "eventType": session_type,
            }
        },
        "combo": {
            "trackDisplayName": track_display_name or None,
            "carScreenName": car_screen_name or None,
        },
        "latestSample": {
            "focusCarIdx": focus_idx,
            "focusLapDistPct": (reference or {}).get("lapDistPct"),
            "focusPosition": (reference or {}).get("overallPosition"),
            "focusClassPosition": (reference or {}).get("classPosition"),
            "sessionTime": raw.get("SessionTime"),
            "sessionState": raw.get("SessionState"),
        },
        "fuel": {
            "fuelLevelLiters": fuel_level,
            "fuelLevelPercent": fuel_pct,
            "fuelUsePerHourKg": positive(raw.get("FuelUsePerHour")),
        },
        "proximity": {},
        "leaderGap": {},
        "models": {
            "reference": {
                "hasData": reference is not None,
                "quality": "capture-derived" if reference is not None else "unavailable",
                "playerCarIdx": player_idx,
                "focusCarIdx": focus_idx,
                "focusIsPlayer": focus_idx is not None and player_idx is not None and focus_idx == player_idx,
                "hasExplicitNonPlayerFocus": focus_idx is not None and player_idx is not None and focus_idx != player_idx,
                "referenceCarClass": (reference or {}).get("carClass"),
                "lapDistPct": (reference or {}).get("lapDistPct"),
                "onPitRoad": on_pit_road,
                "isOnTrack": raw.get("IsOnTrack"),
                "isInGarage": raw.get("IsInGarage"),
                "playerCarInPitStall": raw.get("PlayerCarInPitStall"),
            },
            "session": {
                "hasData": True,
                "quality": "capture-derived",
                "sessionType": session_type,
                "sessionName": str((selected or {}).get("SessionName") or session_type),
                "eventType": session_type,
                "currentSessionNum": raw.get("SessionNum"),
                "sessionState": raw.get("SessionState"),
                "sessionPhase": replay_frame.get("sessionPhase"),
                "sessionTimeSeconds": raw.get("SessionTime"),
                "sessionTimeRemainSeconds": session_time_remain,
                "sessionLapsRemainEx": raw.get("SessionLapsRemainEx"),
                "sessionLapsTotal": raw.get("SessionLapsTotal"),
                "trackDisplayName": track_display_name or None,
                "carScreenName": car_screen_name or None,
            },
            "driverDirectory": {
                "hasData": bool(drivers),
                "quality": "capture-derived" if drivers else "unavailable",
                "focusCarIdx": focus_idx,
                "playerCarIdx": player_idx,
                "drivers": live_driver_rows(drivers),
            },
            "coverage": {
                "hasData": True,
                "quality": "capture-derived",
                "timingRowCount": len(timing_live),
                "scoringRowCount": len(scoring_live),
                "scoringSource": selected_source,
            },
            "scoring": {
                "hasData": bool(scoring_live),
                "quality": "capture-derived" if scoring_live else "unavailable",
                "referenceCarIdx": focus_idx,
                "source": selected_source,
                "rows": scoring_live,
            },
            "timing": {
                "hasData": bool(timing_live),
                "quality": "capture-derived" if timing_live else "unavailable",
                "focusCarIdx": focus_idx,
                "focusRow": enrich_live_row(reference, drivers.get(focus_idx), focus_idx, player_idx) if reference and focus_idx is not None else None,
                "playerRow": enrich_live_row(timing_rows[player_idx], drivers.get(player_idx), focus_idx, player_idx) if player_idx in timing_rows else None,
                "overallRows": timing_live,
                "classRows": [row for row in timing_live if reference is None or row.get("carClass") == reference.get("carClass")],
            },
            "relative": {
                "hasData": bool(relative_live),
                "quality": "capture-derived" if relative_live else "unavailable",
                "referenceCarIdx": focus_idx,
                "rows": relative_live,
            },
            "spatial": live_spatial_model(raw, relative_live, reference, track_length),
            "raceEvents": {
                "hasData": True,
                "quality": "capture-derived",
                "isOnTrack": raw.get("IsOnTrack"),
                "isInGarage": raw.get("IsInGarage"),
                "isGarageVisible": raw.get("IsInGarage"),
                "lapDistPct": (reference or {}).get("lapDistPct") or raw.get("LapDistPct"),
                "onPitRoad": on_pit_road,
            },
            "trackMap": {"hasData": True, "quality": "capture-derived", "sectors": []},
            "fuelPit": {
                "hasData": any(raw.get(name) is not None for name in (
                    "FuelLevel",
                    "FuelLevelPct",
                    "FuelUsePerHour",
                    "PitSvFlags",
                    "PitSvFuel",
                    "PitRepairLeft",
                    "PitOptRepairLeft",
                    "PlayerCarPitSvStatus",
                    "TireSetsUsed",
                    "FastRepairUsed",
                )) or on_pit_road,
                "quality": "capture-derived",
                "fuel": {
                    "fuelLevelLiters": fuel_level,
                    "fuelLevelPercent": fuel_pct,
                    "fuelUsePerHourKg": positive(raw.get("FuelUsePerHour")),
                },
                "onPitRoad": on_pit_road,
                "pitstopActive": raw.get("PitstopActive") is True,
                "playerCarInPitStall": raw.get("PlayerCarInPitStall") is True,
                "teamOnPitRoad": array_value(values, "CarIdxOnPitRoad", player_idx) if player_idx is not None else None,
                "pitServiceStatus": raw.get("PlayerCarPitSvStatus") if isinstance(raw.get("PlayerCarPitSvStatus"), int) else None,
                "pitServiceFlags": raw.get("PitSvFlags") if isinstance(raw.get("PitSvFlags"), int) else None,
                "pitServiceFuelLiters": positive(raw.get("PitSvFuel")),
                "pitRepairLeftSeconds": valid_non_negative(raw.get("PitRepairLeft")),
                "pitOptRepairLeftSeconds": valid_non_negative(raw.get("PitOptRepairLeft")),
                "tireSetsUsed": raw.get("TireSetsUsed") if isinstance(raw.get("TireSetsUsed"), int) else None,
                "fastRepairUsed": raw.get("FastRepairUsed") if isinstance(raw.get("FastRepairUsed"), int) else None,
                "teamFastRepairsUsed": array_value(values, "CarIdxFastRepairsUsed", player_idx) if player_idx is not None else None,
            },
            "raceProgress": {
                "hasData": reference is not None,
                "quality": "capture-derived" if reference is not None else "unavailable",
                "referenceCarProgressLaps": (
                    (reference.get("lapCompleted") + reference.get("lapDistPct"))
                    if reference and isinstance(reference.get("lapCompleted"), int) and finite(reference.get("lapDistPct"))
                    else None
                ),
            },
            "raceProjection": {"hasData": reference is not None, "quality": "capture-derived" if reference is not None else "unavailable"},
            "weather": {
                "hasData": any(raw.get(name) is not None for name in ("AirTemp", "TrackTempCrew", "TrackTemp", "TrackWetness", "WeatherDeclaredWet", "Skies", "Precipitation", "WindVel", "WindDir", "RelativeHumidity", "FogLevel")),
                "quality": "capture-derived",
                "airTempC": raw.get("AirTemp") if finite(raw.get("AirTemp")) else None,
                "trackTempCrewC": raw.get("TrackTempCrew") if finite(raw.get("TrackTempCrew")) else raw.get("TrackTemp") if finite(raw.get("TrackTemp")) else None,
                "trackWetness": track_wetness,
                "weatherDeclaredWet": raw.get("WeatherDeclaredWet") if isinstance(raw.get("WeatherDeclaredWet"), bool) else None,
                "trackWetnessLabel": track_wetness_label(track_wetness),
                "skies": raw.get("Skies") if isinstance(raw.get("Skies"), int) else None,
                "skiesLabel": skies_label(raw.get("Skies"), session_data),
                "precipitationPercent": valid_non_negative(raw.get("Precipitation")),
                "windVelocityMetersPerSecond": valid_non_negative(raw.get("WindVel")),
                "windDirectionRadians": raw.get("WindDir") if finite(raw.get("WindDir")) else None,
                "relativeHumidityPercent": valid_non_negative(raw.get("RelativeHumidity")),
                "fogLevelPercent": valid_non_negative(raw.get("FogLevel")),
                "rubberState": str((selected or {}).get("SessionTrackRubberState") or "").strip() or None,
            },
            "inputs": {
                "hasData": any(raw.get(name) is not None for name in ("Throttle", "Brake", "Clutch", "ClutchRaw", "SteeringWheelAngle", "Gear", "Speed", "RPM", "EngineWarnings", "Voltage", "WaterTemp", "FuelPress", "OilTemp", "OilPress")),
                "quality": "capture-derived",
                "speedMetersPerSecond": raw.get("Speed") if finite(raw.get("Speed")) else None,
                "playerTireCompound": raw.get("PlayerTireCompound") if isinstance(raw.get("PlayerTireCompound"), int) else None,
                "hasPedalInputs": any(unit_interval(raw.get(name)) is not None for name in ("Throttle", "Brake", "Clutch", "ClutchRaw")),
                "hasSteeringInput": finite(raw.get("SteeringWheelAngle")),
                "gear": raw.get("Gear") if isinstance(raw.get("Gear"), int) else None,
                "rpm": raw.get("RPM") if finite(raw.get("RPM")) else None,
                "throttle": unit_interval(raw.get("Throttle")),
                "brake": unit_interval(raw.get("Brake")),
                "clutch": clutch,
                "steeringWheelAngle": raw.get("SteeringWheelAngle") if finite(raw.get("SteeringWheelAngle")) else None,
                "brakeAbsActive": raw.get("BrakeABSactive") if isinstance(raw.get("BrakeABSactive"), bool) else None,
                "engineWarnings": raw.get("EngineWarnings") if isinstance(raw.get("EngineWarnings"), int) else None,
                "voltage": raw.get("Voltage") if finite(raw.get("Voltage")) else None,
                "waterTempC": raw.get("WaterTemp") if finite(raw.get("WaterTemp")) else None,
                "fuelPressureBar": raw.get("FuelPress") if finite(raw.get("FuelPress")) else None,
                "oilTempC": raw.get("OilTemp") if finite(raw.get("OilTemp")) else None,
                "oilPressureBar": raw.get("OilPress") if finite(raw.get("OilPress")) else None,
            },
        },
    }


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
    gridded_car_idxs.clear()
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
    selected_source, scoring_rows = selected_scoring_rows(session_data, raw, selected, all_timing_cars(values))
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
    allow_leader_progress = selected_source == "session-results" and isinstance(raw.get("SessionState"), int) and raw["SessionState"] >= 4
    row_budget = maximum_rows if not include_other else min(64, maximum_rows + len(groups) + (len(groups) - 1) * visible_other)
    reserved_other = (len(groups) - 1) * (1 + visible_other) if include_other else 0
    primary_limit = max(2 if include_headers else 1, row_budget - reserved_other)

    display_rows: list[dict[str, Any]] = []
    for key, rows in groups:
        rows = order_class_rows(rows)
        is_primary = key == primary_key
        if not is_primary and not include_other:
            continue
        group_limit = primary_limit if is_primary else (1 + visible_other if include_headers else visible_other)
        if include_headers:
            first_driver = drivers.get(rows[0]["carIdx"])
            display_rows.append(header_row(class_label(first_driver, key), f"{len(rows)} cars", class_color(first_driver)))
            group_limit -= 1
        ordered_rows = rows
        for row in select_around_reference(ordered_rows, ref_idx if is_primary else None, group_limit, preserve_first=is_primary):
            timing = timing_rows.get(row["carIdx"])
            display_rows.append(
                car_display_row(
                    row,
                    timing,
                    ref_idx,
                    rows,
                    timing_rows,
                    drivers,
                    session_data=session_data,
                    session_kind=session_kind,
                    session_state=raw.get("SessionState") if isinstance(raw.get("SessionState"), int) else None,
                    allow_leader_progress=allow_leader_progress,
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
    status = standings_status(status_prefix, car_count, len(scoring_rows))
    if selected_source.startswith("starting-grid"):
        source = "source: starting grid + live timing" if timing_rows else "source: starting grid"
    else:
        source = "source: scoring snapshot + live timing"
    return status, source, display_rows


def order_class_rows(rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return sorted(
        rows,
        key=lambda row: (
            row.get("classPosition") or 999,
            row.get("overallPosition") or 999,
            row["carIdx"],
        ),
    )


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
    selected = current_session(session_data)
    session_kind = classify_session_kind(str((selected or {}).get("SessionType") or (selected or {}).get("SessionName") or ""))
    session_state = raw.get("SessionState") if isinstance(raw.get("SessionState"), int) else None
    allow_leader_progress = session_kind == "race" and isinstance(session_state, int) and session_state >= 4
    rows = sorted(
        timing_rows.values(),
        key=lambda row: (
            row.get("classPosition") or 999,
            row.get("overallPosition") or 999,
            row.get("carIdx") or 999,
        ),
    )
    rows = select_around_reference(rows, ref_idx, maximum_rows, preserve_first=True)
    display_rows = [
        car_display_row(
            timing,
            timing,
            ref_idx,
            rows,
            timing_rows,
            drivers,
            session_data=session_data,
            session_kind=session_kind,
            session_state=session_state,
            allow_leader_progress=allow_leader_progress)
        for timing in rows
    ]
    reference = next((row for row in display_rows if row.get("isReference")), None)
    status_prefix = reference["cells"][0] if reference and reference.get("cells") else None
    status = standings_status(status_prefix, len(display_rows), None)
    return status, "source: live timing telemetry", display_rows


def standings_status(class_position: str | None, shown_rows: int, total_rows: int | None) -> str:
    shown_text = f"{shown_rows}/{total_rows} shown" if total_rows is not None else f"{shown_rows} shown"
    if class_position and class_position != "--":
        return f"P{class_position} | {shown_text}"
    return shown_text


def select_around_reference(rows: list[dict[str, Any]], ref_idx: int | None, limit: int, preserve_first: bool = False) -> list[dict[str, Any]]:
    ordered = list(rows)
    if limit <= 0 or len(ordered) <= limit:
        return ordered[: max(0, limit)]
    if ref_idx is None:
        return ordered[:limit]
    ref_pos = next((index for index, row in enumerate(ordered) if row["carIdx"] == ref_idx), -1)
    if ref_pos < 0:
        return ordered[:limit]
    if preserve_first and ref_pos > 0:
        if limit == 1:
            return ordered[:1]
        return ordered[:1] + select_around_reference(ordered[1:], ref_idx, limit - 1)
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
    session_data: dict[str, Any] | None = None,
    session_kind: str | None = None,
    session_state: int | None = None,
    allow_leader_progress: bool = False,
    class_position_override: int | None = None,
    interval_override: str | None = None,
    is_pending_grid: bool = False,
) -> dict[str, Any]:
    car_idx = row["carIdx"]
    timing = timing or {}
    driver = drivers.get(car_idx)
    ref_timing = timing_rows.get(ref_idx) if ref_idx is not None else None
    class_position = row.get("classPosition") or timing.get("classPosition")
    gap_seconds, gap_laps, interval_seconds, interval_laps = class_gap_and_interval(
        row,
        timing,
        group_rows,
        timing_rows,
        session_data,
        session_kind,
        session_state,
        drivers,
    )
    display_class_position = class_position_override or class_position
    if display_class_position == 1:
        gap = (leader_progress(row, timing) or "Leader") if allow_leader_progress else "Leader"
    else:
        gap = signed_gap(gap_seconds, gap_laps)
    interval = interval_override or ("0.0" if display_class_position == 1 else signed_gap(interval_seconds, interval_laps))
    return {
        "cells": [
            str(display_class_position) if display_class_position else "--",
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


def leader_progress(row: dict[str, Any], timing: dict[str, Any] | None) -> str | None:
    timing = timing or {}
    lap_completed = timing.get("lapCompleted")
    lap_dist_pct = valid_lap_dist_pct(timing.get("lapDistPct"))
    if isinstance(lap_completed, int) and lap_completed >= 0:
        display_lap = lap_completed + (1 if lap_dist_pct is not None and 0.0 < lap_dist_pct < 1.0 else 0)
        return f"Lap {display_lap}" if display_lap > 0 else None
    if isinstance(row.get("lap"), int) and row["lap"] > 0:
        return f"Lap {row['lap']}"
    if isinstance(row.get("lapsComplete"), int) and row["lapsComplete"] > 0:
        return f"{row['lapsComplete']}L"
    return None


def order_scoring_rows_for_display(
    group_rows: list[dict[str, Any]],
    timing_rows: dict[int, dict[str, Any]],
    session_data: dict[str, Any] | None,
    session_kind: str | None,
    session_state: int | None,
    drivers: dict[int, dict[str, Any]],
) -> list[dict[str, Any]]:
    gaps = {
        row["carIdx"]: class_gap_and_interval(
            row,
            timing_rows.get(row["carIdx"]) or {},
            group_rows,
            timing_rows,
            session_data,
            session_kind,
            session_state,
            drivers,
        )[0]
        for row in group_rows
    }
    if sum(1 for gap in gaps.values() if gap is not None and finite(gap) and gap >= 0.0) < 2:
        return group_rows
    return sorted(
        group_rows,
        key=lambda row: (
            gaps.get(row["carIdx"]) if gaps.get(row["carIdx"]) is not None else math.inf,
            row.get("classPosition") or (timing_rows.get(row["carIdx"]) or {}).get("classPosition") or 999,
            row.get("overallPosition") or (timing_rows.get(row["carIdx"]) or {}).get("overallPosition") or 999,
            row["carIdx"],
        ),
    )


def live_intervals_for_ordered_rows(
    ordered_rows: list[dict[str, Any]],
    timing_rows: dict[int, dict[str, Any]],
    session_data: dict[str, Any] | None,
    session_kind: str | None,
    session_state: int | None,
    drivers: dict[int, dict[str, Any]],
) -> dict[int, str]:
    intervals: dict[int, str] = {}
    previous_gap: float | None = None
    for row in ordered_rows:
        gap = class_gap_and_interval(
            row,
            timing_rows.get(row["carIdx"]) or {},
            ordered_rows,
            timing_rows,
            session_data,
            session_kind,
            session_state,
            drivers,
        )[0]
        if gap is None or not finite(gap) or gap < 0.0:
            intervals[row["carIdx"]] = "--"
            continue
        intervals[row["carIdx"]] = "0.0" if previous_gap is None else f"+{max(0.0, gap - previous_gap):.1f}"
        previous_gap = gap
    return intervals


def class_gap_and_interval(
    row: dict[str, Any],
    timing: dict[str, Any],
    group_rows: list[dict[str, Any]],
    timing_rows: dict[int, dict[str, Any]],
    session_data: dict[str, Any] | None,
    session_kind: str | None,
    session_state: int | None,
    drivers: dict[int, dict[str, Any]],
) -> tuple[float | None, float | None, float | None, float | None]:
    class_position = row.get("classPosition") or timing.get("classPosition")
    if class_position == 1:
        return 0.0, None, None, None

    is_race = session_kind == "race"
    allow_timing = not is_race or (isinstance(session_state, int) and session_state >= 4)
    if not allow_timing:
        return None, None, None, None

    ordered = sorted(
        group_rows,
        key=lambda candidate: (
            candidate.get("classPosition") or (timing_rows.get(candidate["carIdx"]) or {}).get("classPosition") or 999,
            candidate.get("overallPosition") or (timing_rows.get(candidate["carIdx"]) or {}).get("overallPosition") or 999,
            candidate["carIdx"],
        ),
    )
    leader = next((candidate for candidate in ordered if (candidate.get("classPosition") or (timing_rows.get(candidate["carIdx"]) or {}).get("classPosition")) == 1), None)
    leader = leader or ordered[0] if ordered else None
    previous = previous_class_row(row["carIdx"], ordered)
    lap_time = class_lap_time_seconds(group_rows, timing_rows)
    gap_seconds, gap_laps = derived_standings_gap(
        timing,
        timing_rows.get(leader["carIdx"]) if leader else None,
        is_race,
        allow_timing,
        lap_time)
    interval_seconds, interval_laps = derived_standings_gap(
        timing,
        timing_rows.get(previous["carIdx"]) if previous else None,
        is_race,
        allow_timing,
        lap_time)
    return gap_seconds, gap_laps, interval_seconds, interval_laps


def previous_class_row(car_idx: int, ordered_rows: list[dict[str, Any]]) -> dict[str, Any] | None:
    for index, candidate in enumerate(ordered_rows):
        if candidate["carIdx"] == car_idx:
            return ordered_rows[index - 1] if index > 0 else None
    return None


def derived_standings_gap(
    timing: dict[str, Any] | None,
    reference_ahead: dict[str, Any] | None,
    is_race: bool,
    allow_timing: bool,
    lap_time_seconds: float | None,
) -> tuple[float | None, float | None]:
    if not timing or not reference_ahead:
        return None, None

    lap_gap = whole_lap_gap(reference_ahead, timing, lap_time_seconds, is_race)
    if lap_gap is not None:
        return None, lap_gap

    projected = estimated_seconds_behind(timing, reference_ahead, is_race, lap_time_seconds)
    if projected is not None:
        return projected, None

    timing_f2 = usable_f2_for_timing(timing, is_race)
    reference_f2 = usable_f2_for_timing(reference_ahead, is_race)
    if allow_timing and timing_f2 is not None and reference_f2 is not None and timing_f2 >= reference_f2:
        return timing_f2 - reference_f2, None

    return None, None


def class_lap_time_seconds(group_rows: list[dict[str, Any]], timing_rows: dict[int, dict[str, Any]]) -> float | None:
    lap_times: list[float] = []
    for row in group_rows:
        timing = timing_rows.get(row["carIdx"]) or row
        for key in ("lastLapTimeSeconds", "bestLapTimeSeconds"):
            seconds = valid_timing_seconds(timing.get(key))
            if seconds is not None and 20.0 <= seconds <= 300.0:
                lap_times.append(seconds)
                break
    if not lap_times:
        return None
    return statistics.median(lap_times)


def whole_lap_gap(
    reference_ahead: dict[str, Any],
    timing: dict[str, Any],
    lap_time_seconds: float | None,
    is_race: bool,
) -> float | None:
    reference_lap = reference_ahead.get("lapCompleted")
    timing_lap = timing.get("lapCompleted")
    if isinstance(reference_lap, int) and isinstance(timing_lap, int):
        laps = reference_lap - timing_lap
        return float(laps) if laps > 0 else None
    return inferred_whole_lap_gap(reference_ahead, timing, lap_time_seconds, is_race)


def inferred_whole_lap_gap(
    reference_ahead: dict[str, Any],
    timing: dict[str, Any],
    lap_time_seconds: float | None,
    is_race: bool,
) -> float | None:
    if not is_race or valid_timing_seconds(lap_time_seconds) is None:
        return None
    timing_f2 = usable_f2_for_timing(timing, is_race)
    reference_f2 = usable_f2_for_timing(reference_ahead, is_race)
    projected = estimated_seconds_behind(timing, reference_ahead, is_race, lap_time_seconds)
    if timing_f2 is None or reference_f2 is None or projected is None or timing_f2 < reference_f2:
        return None
    lap_ratio = (timing_f2 - reference_f2 - projected) / float(lap_time_seconds)
    if lap_ratio < 0.85:
        return None
    nearest_lap = round(lap_ratio)
    return float(nearest_lap) if nearest_lap >= 1 and abs(lap_ratio - nearest_lap) <= 0.35 else None


def estimated_seconds_behind(
    timing: dict[str, Any],
    reference_ahead: dict[str, Any],
    is_race: bool,
    lap_time_seconds: float | None,
) -> float | None:
    if not is_race or has_different_completed_lap(timing, reference_ahead):
        return None

    relative_seconds = estimated_relative_seconds(timing, reference_ahead, lap_time_seconds)
    if relative_seconds is None or relative_seconds > 0.0:
        return None
    return max(0.0, -relative_seconds)


def has_different_completed_lap(row: dict[str, Any], reference_ahead: dict[str, Any]) -> bool:
    return isinstance(row.get("lapCompleted"), int) and isinstance(reference_ahead.get("lapCompleted"), int) and row.get("lapCompleted") != reference_ahead.get("lapCompleted")


def estimated_relative_seconds(
    row: dict[str, Any],
    reference_ahead: dict[str, Any],
    lap_time_seconds: float | None,
) -> float | None:
    row_estimated = valid_timing_seconds(row.get("estimatedTimeSeconds"))
    reference_estimated = valid_timing_seconds(reference_ahead.get("estimatedTimeSeconds"))
    row_lap_dist = valid_lap_dist_pct(row.get("lapDistPct"))
    reference_lap_dist = valid_lap_dist_pct(reference_ahead.get("lapDistPct"))
    if row_estimated is None or reference_estimated is None or row_lap_dist is None or reference_lap_dist is None:
        return None

    relative_laps = row_lap_dist - reference_lap_dist
    if relative_laps > 0.5:
        relative_laps -= 1.0
    elif relative_laps < -0.5:
        relative_laps += 1.0

    seconds = row_estimated - reference_estimated
    if lap_time_seconds is not None and valid_timing_seconds(lap_time_seconds) is not None:
        if seconds > lap_time_seconds / 2.0:
            seconds -= lap_time_seconds
        elif seconds < -lap_time_seconds / 2.0:
            seconds += lap_time_seconds

    if not finite(seconds):
        return None

    timing_sign = 1 if seconds > 0 else -1 if seconds < 0 else 0
    lap_sign = 1 if relative_laps > 0 else -1 if relative_laps < 0 else 0
    if timing_sign and lap_sign and timing_sign != lap_sign:
        return None

    if lap_time_seconds is not None and valid_timing_seconds(lap_time_seconds) is not None:
        lap_based_seconds = abs(relative_laps * lap_time_seconds)
        maximum_delta = max(5.0, min(lap_time_seconds / 2.0, lap_based_seconds + 10.0))
        return seconds if abs(seconds) <= maximum_delta else None

    return seconds if abs(seconds) <= 60.0 else None



def usable_f2_for_timing(timing: dict[str, Any], is_race: bool) -> float | None:
    f2 = valid_non_negative(timing.get("f2TimeSeconds"))
    if f2 is None:
        return None
    overall_position = timing.get("overallPosition")
    class_position = timing.get("classPosition")
    if not is_race:
        if f2 == 0.0 and class_position == 1:
            return 0.0
        return valid_timing_seconds(f2)
    if f2 == 0.0:
        return 0.0 if overall_position == 1 or class_position == 1 else None
    if is_race and class_position != 1 and f2 < 0.1:
        return None
    return None if is_race_f2_placeholder(f2, overall_position) else f2


def is_race_f2_placeholder(f2: float | None, overall_position: Any) -> bool:
    if f2 is None or not finite(f2) or not isinstance(overall_position, int) or overall_position <= 1:
        return False
    return abs(float(f2) - ((overall_position - 1) / 1000.0)) <= 0.00002


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
    replay_index: int,
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
    replay_frame = {
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
    replay_frame["live"] = build_live_snapshot(
        replay_index,
        replay_frame,
        raw,
        session_data,
        values,
        gridded_car_idxs,
    )
    return replay_frame


def annotate_source_cadence(frames: list[dict[str, Any]]) -> dict[str, Any]:
    if not frames:
        return {
            "basis": "raw-capture frame header sessionTime",
            "selectedFrameCount": 0,
            "gapToLeaderMissingSegmentThresholdSeconds": GAP_TO_LEADER_MISSING_SEGMENT_THRESHOLD_SECONDS,
            "denseForGapToLeader": False,
        }

    first_session_time = first_finite(frame.get("sessionTimeSeconds") for frame in frames)
    first_captured_ms = first_finite(frame.get("capturedUnixMs") for frame in frames)
    previous_frame: dict[str, Any] | None = None
    session_deltas: list[float] = []
    captured_deltas: list[float] = []
    frame_index_deltas: list[int] = []
    non_monotonic_session_time = False

    for frame in frames:
        session_time = frame.get("sessionTimeSeconds")
        captured_ms = frame.get("capturedUnixMs")
        if first_session_time is not None and finite(session_time):
            frame["sourceElapsedSeconds"] = compact_number(float(session_time) - first_session_time)
        elif first_captured_ms is not None and finite(captured_ms):
            frame["sourceElapsedSeconds"] = compact_number((float(captured_ms) - first_captured_ms) / 1000.0)

        if previous_frame is not None:
            previous_session_time = previous_frame.get("sessionTimeSeconds")
            previous_captured_ms = previous_frame.get("capturedUnixMs")
            if finite(session_time) and finite(previous_session_time):
                delta = float(session_time) - float(previous_session_time)
                frame["sourceSessionDeltaSeconds"] = compact_number(delta)
                if delta < 0:
                    non_monotonic_session_time = True
                else:
                    session_deltas.append(delta)
            if finite(captured_ms) and finite(previous_captured_ms):
                delta = (float(captured_ms) - float(previous_captured_ms)) / 1000.0
                frame["sourceCapturedDeltaSeconds"] = compact_number(delta)
                if delta > 0:
                    captured_deltas.append(delta)
            if isinstance(frame.get("frameIndex"), int) and isinstance(previous_frame.get("frameIndex"), int):
                delta = frame["frameIndex"] - previous_frame["frameIndex"]
                frame["sourceFrameDelta"] = delta
                if delta > 0:
                    frame_index_deltas.append(delta)

        previous_frame = frame

    source_elapsed = [
        float(frame["sourceElapsedSeconds"])
        for frame in frames
        if finite(frame.get("sourceElapsedSeconds"))
    ]
    max_session_delta = max(session_deltas) if session_deltas else 0.0
    return {
        "basis": "raw-capture frame header sessionTime",
        "selectedFrameCount": len(frames),
        "gapToLeaderMissingSegmentThresholdSeconds": GAP_TO_LEADER_MISSING_SEGMENT_THRESHOLD_SECONDS,
        "denseForGapToLeader": len(frames) < 2 or (bool(session_deltas) and max_session_delta <= GAP_TO_LEADER_MISSING_SEGMENT_THRESHOLD_SECONDS),
        "hasNonMonotonicSessionTime": non_monotonic_session_time,
        "sourceElapsedSeconds": summarize_numeric(source_elapsed),
        "sourceSessionDeltaSeconds": summarize_numeric(session_deltas),
        "sourceCapturedDeltaSeconds": summarize_numeric(captured_deltas),
        "sourceFrameDelta": summarize_numeric(frame_index_deltas, digits=0),
    }


def first_finite(values: Iterable[Any]) -> float | None:
    for value in values:
        if finite(value):
            return float(value)
    return None


def summarize_numeric(values: Iterable[Any], digits: int = 3) -> dict[str, Any]:
    numbers = sorted(float(value) for value in values if finite(value))
    if not numbers:
        return {"count": 0, "min": None, "median": None, "max": None}
    return {
        "count": len(numbers),
        "min": compact_number(numbers[0], digits),
        "median": compact_number(statistics.median(numbers), digits),
        "max": compact_number(numbers[-1], digits),
    }


def enforce_dense_cadence(cadence: dict[str, Any], allow_sparse_review: bool) -> None:
    if allow_sparse_review or cadence.get("denseForGapToLeader") is True:
        return

    max_delta = ((cadence.get("sourceSessionDeltaSeconds") or {}).get("max"))
    threshold = cadence.get("gapToLeaderMissingSegmentThresholdSeconds") or GAP_TO_LEADER_MISSING_SEGMENT_THRESHOLD_SECONDS
    detail = f"max SessionTime delta {max_delta}s" if max_delta is not None else "missing positive SessionTime deltas"
    raise SystemExit(
        "Selected replay frames are too sparse for Gap To Leader browser validation "
        f"({detail}; threshold {threshold}s). Export denser raw frames with a smaller --stride "
        "or larger --max-frames. Use --allow-sparse-review only for non-graph/table review; "
        "do not alter graph segmentation to connect sparse samples."
    )


def export_capture(args: argparse.Namespace) -> dict[str, Any]:
    capture_dir = args.capture
    manifest = read_json(capture_dir / "capture-manifest.json")
    schema = load_schema(capture_dir)
    updates, snapshots, latest = load_session_snapshots(capture_dir)
    manifest_frame_count = int(manifest.get("frameCount") or 0)
    buffer_length = int(manifest.get("bufferLength") or 0)
    telemetry_path = capture_dir / str(manifest.get("telemetryFile") or "telemetry.bin")
    frame_count = readable_frame_count(telemetry_path, buffer_length, manifest_frame_count)
    frames = []
    gridded_car_idxs: set[int] = set()
    requested_indexes = frame_indexes(frame_count, args.max_frames, args.stride, args.start_frame)
    for replay_index, requested_index in enumerate(requested_indexes):
        frame = read_capture_frame(telemetry_path, buffer_length, requested_index)
        if frame is None:
            continue
        header, payload = frame
        session_data = session_info_for_update(header["sessionInfoUpdate"], updates, snapshots, latest)
        replay_frame = build_frame(
            replay_index,
            capture_dir,
            manifest,
            schema,
            header,
            payload,
            session_data,
            args.other_class_rows,
            args.maximum_rows,
            gridded_car_idxs)
        if args.start_relative_seconds is not None and args.step_seconds is not None:
            replay_frame["raceStartRelativeSeconds"] = args.start_relative_seconds + replay_index * args.step_seconds
        frames.append(replay_frame)
    cadence = annotate_source_cadence(frames)
    enforce_dense_cadence(cadence, args.allow_sparse_review)
    alignment = None
    if args.start_relative_seconds is not None and args.step_seconds is not None and frames:
        alignment = {
            "startFrameIndex": requested_indexes[0] if requested_indexes else None,
            "sourceStrideFrames": args.stride,
            "startRelativeSeconds": args.start_relative_seconds,
            "endRelativeSeconds": frames[-1].get("raceStartRelativeSeconds"),
            "stepSeconds": args.step_seconds,
            "basis": "review navigation only; source cadence uses raw capture SessionTime",
            "selectedFrameCount": len(frames),
        }
    return {
        "schemaVersion": 1,
        "kind": "standings-browser-replay",
        "source": {
            "captureId": str(manifest.get("captureId") or capture_dir.name),
            "sourceCategory": source_category(str(manifest.get("captureId") or capture_dir.name)),
            "captureDirectory": str(capture_dir),
            "manifestFrameCount": manifest_frame_count,
            "frameCount": frame_count,
            "sampledFrameCount": len(frames),
            "startedAtUtc": manifest.get("startedAtUtc"),
            "finishedAtUtc": manifest.get("finishedAtUtc"),
            "alignment": alignment,
            "cadence": cadence,
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
    parser.add_argument("--start-relative-seconds", type=float, default=None, help="Relative race-start seconds assigned to the first exported frame.")
    parser.add_argument("--step-seconds", type=float, default=None, help="Relative seconds added per exported frame.")
    parser.add_argument("--allow-sparse-review", action="store_true", help="Permit sparse exports only for non-graph review; Gap To Leader validation will reject them.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    replay = export_capture(args)
    write_json(args.output, replay)
    print(f"Wrote {len(replay['frames'])} standings replay frames to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
