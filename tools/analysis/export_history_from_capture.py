#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import os
import re
import statistics
import struct
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml


FRAME_HEADER = struct.Struct("<qiiidi")
FILE_HEADER_BYTES = 32
PRIMARY_CAPTURE_ID = "capture-20260426-130334-932"
PRIMARY_CAPTURE = Path("captures") / PRIMARY_CAPTURE_ID
DEFAULT_OUTPUT_ROOT = Path("history") / "baseline"

WEATHER_FIELDS = [
    ("trackTempC", "TrackTemp"),
    ("trackTempCrewC", "TrackTempCrew"),
    ("airTempC", "AirTemp"),
    ("trackWetness", "TrackWetness"),
    ("skies", "Skies"),
    ("windVelMetersPerSecond", "WindVel"),
    ("windDirRadians", "WindDir"),
    ("relativeHumidityPercent", "RelativeHumidity"),
    ("fogLevelPercent", "FogLevel"),
    ("precipitationPercent", "Precipitation"),
    ("airDensityKgPerCubicMeter", "AirDensity"),
    ("airPressurePa", "AirPressure"),
    ("solarAltitudeRadians", "SolarAltitude"),
    ("solarAzimuthRadians", "SolarAzimuth"),
    ("weatherDeclaredWet", "WeatherDeclaredWet"),
]


def slug(value: Any) -> str:
    text = str(value or "").strip().lower()
    if not text:
        return "unknown"

    output: list[str] = []
    previous_separator = False
    for character in text:
        if "a" <= character <= "z" or "0" <= character <= "9":
            output.append(character)
            previous_separator = False
            continue

        if not previous_separator:
            output.append("-")
            previous_separator = True

    return "".join(output).strip("-") or "unknown"


def first_number(value: Any) -> float | None:
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return float(value)
    if isinstance(value, str):
        match = re.search(r"[-+]?\d+(?:\.\d+)?", value)
        if match:
            return float(match.group(0))
    return None


def read_bool(payload: bytes, offset: int) -> bool:
    return payload[offset] != 0


def read_int(payload: bytes, offset: int) -> int:
    return struct.unpack_from("<i", payload, offset)[0]


def read_float(payload: bytes, offset: int) -> float:
    return struct.unpack_from("<f", payload, offset)[0]


def read_double(payload: bytes, offset: int) -> float:
    return struct.unpack_from("<d", payload, offset)[0]


def read_value(payload: bytes, schema: dict[str, Any], name: str, index: int | None = None) -> Any:
    variable = schema[name]
    offset = int(variable["offset"])
    if index is not None:
        offset += index * int(variable["byteSize"])

    type_name = variable["typeName"]
    if type_name == "irBool":
        return read_bool(payload, offset)
    if type_name == "irInt":
        return read_int(payload, offset)
    if type_name == "irFloat":
        return read_float(payload, offset)
    if type_name == "irDouble":
        return read_double(payload, offset)
    if type_name == "irBitField":
        return struct.unpack_from("<I", payload, offset)[0]
    raise KeyError(f"Unsupported variable type for {name}: {type_name}")


def read_optional_value(payload: bytes, schema: dict[str, Any], name: str, index: int | None = None) -> Any:
    if name not in schema:
        return None
    return read_value(payload, schema, name, index)


def clean_float(value: Any, digits: int = 6) -> float | None:
    if value is None:
        return None
    number = float(value)
    if math.isnan(number) or math.isinf(number):
        return None
    return round(number, digits)


def is_valid_fuel(value: float) -> bool:
    return not math.isnan(value) and not math.isinf(value) and value > 0


def is_green_fuel_sample(sample: dict[str, Any]) -> bool:
    return (
        sample["isOnTrack"]
        and not sample["onPitRoad"]
        and not sample["isInGarage"]
        and sample["speedMetersPerSecond"] > 5
        and is_valid_fuel(sample["fuelLevelLiters"])
    )


def lap_position(sample: dict[str, Any]) -> float:
    lap = sample["lap"] if sample["lap"] >= 0 else 0
    return lap + max(0.0, min(1.0, sample["lapDistPct"]))


def distance_delta_laps(previous: dict[str, Any], current: dict[str, Any]) -> float:
    delta = lap_position(current) - lap_position(previous)
    return delta if 0 < delta < 0.5 else 0.0


def is_pit_context(previous: dict[str, Any], current: dict[str, Any]) -> bool:
    return (
        previous["onPitRoad"]
        or current["onPitRoad"]
        or previous["pitstopActive"]
        or current["pitstopActive"]
        or previous["playerCarInPitStall"]
        or current["playerCarInPitStall"]
    )


def surface_moisture_class(track_wetness: Any) -> str:
    if track_wetness is None:
        return "unknown"
    if track_wetness <= 1:
        return "dry"
    if track_wetness <= 3:
        return "damp"
    return "wet"


def weather_state(sample: dict[str, Any]) -> dict[str, Any]:
    return {
        "trackWetness": sample.get("trackWetness"),
        "surfaceMoistureClass": surface_moisture_class(sample.get("trackWetness")),
        "weatherDeclaredWet": sample.get("weatherDeclaredWet"),
        "skies": sample.get("skies"),
        "precipitationPercent": clean_float(sample.get("precipitationPercent"), 2),
        "relativeHumidityPercent": clean_float(sample.get("relativeHumidityPercent"), 2),
        "fogLevelPercent": clean_float(sample.get("fogLevelPercent"), 2),
        "trackTempCrewC": clean_float(sample.get("trackTempCrewC"), 1),
        "airTempC": clean_float(sample.get("airTempC"), 1),
    }


def running_metric(value: float | int | None) -> dict[str, Any]:
    if value is None:
        return {"sampleCount": 0, "mean": None, "minimum": None, "maximum": None}

    number = float(value)
    return {
        "sampleCount": 1,
        "mean": clean_float(number),
        "minimum": clean_float(number),
        "maximum": clean_float(number),
    }


def collapse_pit_windows(windows: list[dict[str, Any]], max_gap_seconds: float = 120.0) -> list[dict[str, Any]]:
    collapsed: list[dict[str, Any]] = []
    for window in windows:
        if not collapsed:
            collapsed.append(dict(window))
            continue

        previous = collapsed[-1]
        previous_exit = previous.get("exitRaceTimeSeconds")
        current_entry = window.get("entryRaceTimeSeconds")
        if previous_exit is not None and current_entry is not None and current_entry - previous_exit <= max_gap_seconds:
            previous["exitRaceTimeSeconds"] = window.get("exitRaceTimeSeconds")
            previous["exitLapCompleted"] = window.get("exitLapCompleted")
            previous["exitPosition"] = window.get("exitPosition")
            previous["exitClassPosition"] = window.get("exitClassPosition")
            if previous.get("entryRaceTimeSeconds") is not None and previous.get("exitRaceTimeSeconds") is not None:
                previous["durationSeconds"] = clean_float(
                    previous["exitRaceTimeSeconds"] - previous["entryRaceTimeSeconds"],
                    3,
                )
            continue

        collapsed.append(dict(window))

    return collapsed


def load_yaml(path: Path) -> dict[str, Any]:
    return yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}


def load_schema(capture_dir: Path) -> dict[str, dict[str, Any]]:
    schema_rows = json.loads((capture_dir / "telemetry-schema.json").read_text(encoding="utf-8"))
    return {row["name"]: row for row in schema_rows}


def select_driver(data: dict[str, Any], car_idx: int) -> dict[str, Any]:
    for driver in (data.get("DriverInfo") or {}).get("Drivers") or []:
        if driver.get("CarIdx") == car_idx:
            return driver
    return {}


def select_session(data: dict[str, Any], session_num: int) -> dict[str, Any]:
    for session in (data.get("SessionInfo") or {}).get("Sessions") or []:
        if session.get("SessionNum") == session_num:
            return session
    return {}


def parse_active_driver_changes(capture_dir: Path, update_timing: dict[int, dict[str, Any]], car_idx: int) -> list[dict[str, Any]]:
    rows: list[tuple[int, str]] = []
    for path in sorted((capture_dir / "session-info").glob("session-*.yaml")):
        match = re.match(r"session-(\d+)\.yaml", path.name)
        if not match:
            continue
        update = int(match.group(1))
        data = load_yaml(path)
        if ((data.get("SessionInfo") or {}).get("CurrentSessionNum")) != 2:
            continue
        driver = select_driver(data, car_idx)
        user_id = driver.get("UserID")
        label = "local-driver" if user_id == (data.get("DriverInfo") or {}).get("DriverUserID") else "teammate-driver"
        if not rows or rows[-1][1] != label:
            rows.append((update, label))

    stints: list[dict[str, Any]] = []
    for index, (start_update, label) in enumerate(rows):
        end_update = rows[index + 1][0] - 1 if index + 1 < len(rows) else None
        start_timing = update_timing.get(start_update) or {}
        end_timing = update_timing.get(end_update) if end_update is not None else None
        if end_timing is None and update_timing:
            end_timing = update_timing[max(update_timing)]
        stints.append(
            {
                "driverRole": label,
                "startUpdate": start_update,
                "endUpdate": end_update,
                "startRaceTimeSeconds": clean_float(start_timing.get("firstSessionTime"), 3),
                "endRaceTimeSeconds": clean_float((end_timing or {}).get("lastSessionTime"), 3),
                "fuelSource": "live_local_scalar" if label == "local-driver" else "unavailable_teammate_stint",
            }
        )
    return stints


def analyze_telemetry(capture_dir: Path, schema: dict[str, dict[str, Any]], car_idx: int, race_session_num: int) -> dict[str, Any]:
    telemetry_path = capture_dir / "telemetry.bin"
    previous: dict[str, Any] | None = None
    latest_conditions: dict[str, Any] = {}
    update_timing: dict[int, dict[str, Any]] = {}
    team_lap_times: list[float] = []
    team_pit_windows: list[dict[str, Any]] = []
    weather_timeline: list[dict[str, Any]] = []
    weather_timeline_truncated = False
    current_pit_window: dict[str, Any] | None = None
    last_team_lap_time: float | None = None
    previous_team_on_pit_road: bool | None = None
    previous_weather_state: dict[str, Any] | None = None

    metrics = {
        "sampleFrameCount": 0,
        "captureDurationSeconds": 0.0,
        "onTrackTimeSeconds": 0.0,
        "pitRoadTimeSeconds": 0.0,
        "movingTimeSeconds": 0.0,
        "validGreenTimeSeconds": 0.0,
        "validDistanceLaps": 0.0,
        "fuelUsedLiters": 0.0,
        "fuelAddedLiters": 0.0,
        "startingFuelLiters": None,
        "endingFuelLiters": None,
        "minimumFuelLiters": None,
        "maximumFuelLiters": None,
        "pitRoadEntryCount": 0,
        "pitServiceCount": 0,
    }
    first_race_ms: int | None = None
    last_race_ms: int | None = None

    with telemetry_path.open("rb") as stream:
        if len(stream.read(FILE_HEADER_BYTES)) != FILE_HEADER_BYTES:
            raise RuntimeError(f"Invalid telemetry header: {telemetry_path}")

        while True:
            frame_header = stream.read(FRAME_HEADER.size)
            if not frame_header:
                break
            if len(frame_header) != FRAME_HEADER.size:
                raise RuntimeError("Truncated telemetry frame header")

            captured_ms, frame_index, session_tick, session_info_update, header_session_time, payload_len = FRAME_HEADER.unpack(frame_header)
            payload = stream.read(payload_len)
            if len(payload) != payload_len:
                raise RuntimeError("Truncated telemetry payload")

            timing = update_timing.setdefault(
                session_info_update,
                {
                    "firstCapturedUnixMs": captured_ms,
                    "firstFrameIndex": frame_index,
                    "firstSessionTime": header_session_time,
                    "lastCapturedUnixMs": captured_ms,
                    "lastFrameIndex": frame_index,
                    "lastSessionTime": header_session_time,
                    "frameCount": 0,
                },
            )
            timing["lastCapturedUnixMs"] = captured_ms
            timing["lastFrameIndex"] = frame_index
            timing["lastSessionTime"] = header_session_time
            timing["frameCount"] += 1

            session_num = read_value(payload, schema, "SessionNum")
            if session_num != race_session_num:
                continue

            metrics["sampleFrameCount"] += 1
            first_race_ms = captured_ms if first_race_ms is None else first_race_ms
            last_race_ms = captured_ms

            sample = {
                "capturedMs": captured_ms,
                "sessionTime": read_value(payload, schema, "SessionTime"),
                "sessionTick": read_value(payload, schema, "SessionTick"),
                "sessionInfoUpdate": session_info_update,
                "isOnTrack": read_value(payload, schema, "IsOnTrack"),
                "isInGarage": read_value(payload, schema, "IsInGarage"),
                "onPitRoad": read_value(payload, schema, "OnPitRoad"),
                "pitstopActive": read_value(payload, schema, "PitstopActive"),
                "playerCarInPitStall": read_value(payload, schema, "PlayerCarInPitStall"),
                "fuelLevelLiters": read_value(payload, schema, "FuelLevel"),
                "fuelLevelPercent": read_value(payload, schema, "FuelLevelPct"),
                "fuelUsePerHourKg": read_value(payload, schema, "FuelUsePerHour"),
                "speedMetersPerSecond": read_value(payload, schema, "Speed"),
                "lap": read_value(payload, schema, "Lap"),
                "lapCompleted": read_value(payload, schema, "LapCompleted"),
                "lapDistPct": read_value(payload, schema, "LapDistPct"),
                "lapLastLapTimeSeconds": read_value(payload, schema, "LapLastLapTime"),
                "lapBestLapTimeSeconds": read_value(payload, schema, "LapBestLapTime"),
                "playerTireCompound": read_value(payload, schema, "PlayerTireCompound"),
            }
            for output_name, sdk_name in WEATHER_FIELDS:
                sample[output_name] = read_optional_value(payload, schema, sdk_name)
            team = {
                "lap": read_value(payload, schema, "CarIdxLap", car_idx),
                "lapCompleted": read_value(payload, schema, "CarIdxLapCompleted", car_idx),
                "lapDistPct": read_value(payload, schema, "CarIdxLapDistPct", car_idx),
                "onPitRoad": read_value(payload, schema, "CarIdxOnPitRoad", car_idx),
                "position": read_value(payload, schema, "CarIdxPosition", car_idx),
                "classPosition": read_value(payload, schema, "CarIdxClassPosition", car_idx),
                "lastLapTime": read_value(payload, schema, "CarIdxLastLapTime", car_idx),
                "bestLapTime": read_value(payload, schema, "CarIdxBestLapTime", car_idx),
            }

            latest_conditions = {
                "trackTempC": sample["trackTempC"],
                "airTempC": sample["airTempC"],
                "trackTempCrewC": sample["trackTempCrewC"],
                "trackWetness": sample["trackWetness"],
                "surfaceMoistureClass": surface_moisture_class(sample["trackWetness"]),
                "weatherDeclaredWet": sample["weatherDeclaredWet"],
                "skies": sample["skies"],
                "windVelMetersPerSecond": sample["windVelMetersPerSecond"],
                "windDirRadians": sample["windDirRadians"],
                "relativeHumidityPercent": sample["relativeHumidityPercent"],
                "fogLevelPercent": sample["fogLevelPercent"],
                "precipitationPercent": sample["precipitationPercent"],
                "airDensityKgPerCubicMeter": sample["airDensityKgPerCubicMeter"],
                "airPressurePa": sample["airPressurePa"],
                "solarAltitudeRadians": sample["solarAltitudeRadians"],
                "solarAzimuthRadians": sample["solarAzimuthRadians"],
                "playerTireCompound": sample["playerTireCompound"],
            }

            current_weather_state = weather_state(sample)
            if current_weather_state != previous_weather_state:
                if len(weather_timeline) < 1_000:
                    weather_timeline.append(
                        {
                            "capturedMs": captured_ms,
                            "sessionTime": clean_float(sample["sessionTime"], 3),
                            **current_weather_state,
                        }
                    )
                else:
                    weather_timeline_truncated = True
                previous_weather_state = current_weather_state

            if is_valid_fuel(sample["fuelLevelLiters"]):
                metrics["startingFuelLiters"] = metrics["startingFuelLiters"] if metrics["startingFuelLiters"] is not None else sample["fuelLevelLiters"]
                metrics["endingFuelLiters"] = sample["fuelLevelLiters"]
                metrics["minimumFuelLiters"] = sample["fuelLevelLiters"] if metrics["minimumFuelLiters"] is None else min(metrics["minimumFuelLiters"], sample["fuelLevelLiters"])
                metrics["maximumFuelLiters"] = sample["fuelLevelLiters"] if metrics["maximumFuelLiters"] is None else max(metrics["maximumFuelLiters"], sample["fuelLevelLiters"])

            lap_time = team["lastLapTime"]
            if 20 < lap_time < 1800 and team["lapCompleted"] > 0:
                if last_team_lap_time is None or abs(lap_time - last_team_lap_time) > 0.001:
                    team_lap_times.append(lap_time)
                    last_team_lap_time = lap_time

            team_on_pit_road = bool(team["onPitRoad"])
            if previous_team_on_pit_road is False and team_on_pit_road:
                metrics["pitRoadEntryCount"] += 1
                current_pit_window = {
                    "entryRaceTimeSeconds": clean_float(sample["sessionTime"], 3),
                    "entryLapCompleted": team["lapCompleted"],
                    "entryPosition": team["position"],
                    "entryClassPosition": team["classPosition"],
                }
            elif previous_team_on_pit_road is True and not team_on_pit_road and current_pit_window is not None:
                current_pit_window.update(
                    {
                        "exitRaceTimeSeconds": clean_float(sample["sessionTime"], 3),
                        "exitLapCompleted": team["lapCompleted"],
                        "exitPosition": team["position"],
                        "exitClassPosition": team["classPosition"],
                    }
                )
                if current_pit_window.get("entryRaceTimeSeconds") is not None:
                    current_pit_window["durationSeconds"] = clean_float(
                        sample["sessionTime"] - current_pit_window["entryRaceTimeSeconds"],
                        3,
                    )
                team_pit_windows.append(current_pit_window)
                current_pit_window = None
            previous_team_on_pit_road = team_on_pit_road

            if previous is not None:
                delta_seconds = sample["sessionTime"] - previous["sessionTime"]
                if 0 < delta_seconds <= 1:
                    if previous["isOnTrack"]:
                        metrics["onTrackTimeSeconds"] += delta_seconds
                    if previous["onPitRoad"]:
                        metrics["pitRoadTimeSeconds"] += delta_seconds
                    if previous["speedMetersPerSecond"] > 1:
                        metrics["movingTimeSeconds"] += delta_seconds
                    if is_green_fuel_sample(previous) and is_valid_fuel(sample["fuelLevelLiters"]):
                        metrics["validGreenTimeSeconds"] += delta_seconds
                        metrics["validDistanceLaps"] += distance_delta_laps(previous, sample)
                        fuel_delta = previous["fuelLevelLiters"] - sample["fuelLevelLiters"]
                        maximum_expected_burn = max(0.05, delta_seconds * 0.10)
                        if 0 < fuel_delta <= maximum_expected_burn:
                            metrics["fuelUsedLiters"] += fuel_delta
                    if is_valid_fuel(previous["fuelLevelLiters"]) and is_valid_fuel(sample["fuelLevelLiters"]):
                        added_fuel = sample["fuelLevelLiters"] - previous["fuelLevelLiters"]
                        if added_fuel > 0.25 and is_pit_context(previous, sample):
                            metrics["fuelAddedLiters"] += added_fuel

            previous = sample

    if current_pit_window is not None:
        team_pit_windows.append(current_pit_window)

    team_pit_windows = collapse_pit_windows(team_pit_windows)

    if first_race_ms is not None and last_race_ms is not None:
        metrics["captureDurationSeconds"] = max(0.0, (last_race_ms - first_race_ms) / 1000)

    metrics["pitRoadEntryCount"] = len(team_pit_windows)
    metrics["pitServiceCount"] = len(team_pit_windows)
    metrics["completedValidLaps"] = len(team_lap_times)
    metrics["averageLapSeconds"] = statistics.mean(team_lap_times) if team_lap_times else None
    metrics["medianLapSeconds"] = statistics.median(team_lap_times) if team_lap_times else None
    metrics["bestLapSeconds"] = min(team_lap_times) if team_lap_times else None
    metrics["fuelPerHourLiters"] = (
        metrics["fuelUsedLiters"] / metrics["validGreenTimeSeconds"] * 3600
        if metrics["validGreenTimeSeconds"] >= 30 and metrics["fuelUsedLiters"] > 0
        else None
    )
    metrics["fuelPerLapLiters"] = (
        metrics["fuelUsedLiters"] / metrics["validDistanceLaps"]
        if metrics["validDistanceLaps"] >= 0.25 and metrics["fuelUsedLiters"] > 0
        else None
    )

    return {
        "metrics": {key: clean_float(value, 6) if isinstance(value, float) else value for key, value in metrics.items()},
        "teamLapTimes": [clean_float(value, 4) for value in team_lap_times],
        "teamPitWindows": team_pit_windows,
        "latestConditions": latest_conditions,
        "weatherTimeline": weather_timeline,
        "weatherTimelineTruncated": weather_timeline_truncated,
        "updateTiming": update_timing,
    }


def quality(metrics: dict[str, Any]) -> dict[str, Any]:
    reasons = [
        "fuel_source_live_local_scalar_partial",
        "teammate_stints_have_no_direct_fuel_scalar",
        "lap_timing_source_car_idx",
        "pit_count_source_car_idx_on_pit_road",
    ]
    if metrics["fuelPerLapLiters"] is not None and metrics["validDistanceLaps"] >= 3 and metrics["completedValidLaps"] >= 3:
        confidence = "high"
    elif metrics["fuelPerLapLiters"] is not None and (metrics["validDistanceLaps"] >= 1 or metrics["completedValidLaps"] >= 1):
        confidence = "medium"
    elif metrics["fuelPerHourLiters"] is not None and metrics["fuelUsedLiters"] >= 0.25:
        confidence = "low"
    else:
        confidence = "none"
    return {
        "confidence": confidence,
        "contributesToBaseline": confidence in {"medium", "high"},
        "reasons": reasons,
    }


def build_summary(capture_dir: Path, output_root: Path) -> tuple[Path, Path, dict[str, Any], dict[str, Any]]:
    manifest = json.loads((capture_dir / "capture-manifest.json").read_text(encoding="utf-8"))
    latest = load_yaml(capture_dir / "latest-session.yaml")
    schema = load_schema(capture_dir)

    weekend = latest["WeekendInfo"]
    driver_info = latest["DriverInfo"]
    current_session_num = latest["SessionInfo"]["CurrentSessionNum"]
    driver_car_idx = driver_info["DriverCarIdx"]
    driver = select_driver(latest, driver_car_idx)
    session = select_session(latest, current_session_num)
    telemetry = analyze_telemetry(capture_dir, schema, driver_car_idx, current_session_num)
    metrics = telemetry["metrics"]
    stints = parse_active_driver_changes(capture_dir, telemetry["updateTiming"], driver_car_idx)
    metrics["stintCount"] = len(stints)

    car = {
        "carId": driver.get("CarID"),
        "carPath": driver.get("CarPath"),
        "carScreenName": driver.get("CarScreenName"),
        "carScreenNameShort": driver.get("CarScreenNameShort"),
        "carClassId": driver.get("CarClassID"),
        "carClassShortName": driver.get("CarClassShortName"),
        "carClassEstLapTimeSeconds": clean_float(driver.get("CarClassEstLapTime")),
        "driverCarFuelMaxLiters": clean_float(driver_info.get("DriverCarFuelMaxLtr")),
        "driverCarFuelKgPerLiter": clean_float(driver_info.get("DriverCarFuelKgPerLtr")),
        "driverCarEstLapTimeSeconds": clean_float(driver_info.get("DriverCarEstLapTime")),
        "driverCarVersion": driver_info.get("DriverCarVersion"),
        "driverGearboxType": driver_info.get("DriverGearboxType"),
        "driverSetupName": None,
        "driverSetupIsModified": None,
    }
    track = {
        "trackId": weekend.get("TrackID"),
        "trackName": weekend.get("TrackName"),
        "trackDisplayName": weekend.get("TrackDisplayName"),
        "trackConfigName": weekend.get("TrackConfigName"),
        "trackLengthKm": first_number(weekend.get("TrackLength")),
        "trackCity": weekend.get("TrackCity"),
        "trackCountry": weekend.get("TrackCountry"),
        "trackNumTurns": weekend.get("TrackNumTurns"),
        "trackType": weekend.get("TrackType"),
        "trackVersion": weekend.get("TrackVersion"),
    }
    session_identity = {
        "currentSessionNum": current_session_num,
        "sessionNum": session.get("SessionNum"),
        "sessionType": session.get("SessionType"),
        "sessionName": session.get("SessionName"),
        "sessionTime": session.get("SessionTime"),
        "sessionLaps": session.get("SessionLaps"),
        "eventType": weekend.get("EventType"),
        "category": weekend.get("Category"),
        "official": bool(weekend.get("Official")),
        "teamRacing": bool(weekend.get("TeamRacing")),
        "seriesId": weekend.get("SeriesID"),
        "seasonId": weekend.get("SeasonID"),
        "sessionId": None,
        "subSessionId": None,
        "buildVersion": weekend.get("BuildVersion"),
    }
    combo = {
        "carKey": slug(f"car-{car['carId']}-{car['carPath'] or car['carScreenName']}"),
        "trackKey": slug(f"track-{track['trackId']}-{track['trackName'] or track['trackDisplayName']}"),
        "sessionKey": slug(session_identity.get("sessionType") or session_identity.get("eventType") or "unknown-session"),
    }
    latest_conditions = telemetry["latestConditions"]
    conditions = {
        "trackTempC": clean_float(latest_conditions.get("trackTempC")),
        "airTempC": clean_float(latest_conditions.get("airTempC")),
        "trackTempCrewC": clean_float(latest_conditions.get("trackTempCrewC")),
        "trackWetness": latest_conditions.get("trackWetness"),
        "surfaceMoistureClass": latest_conditions.get("surfaceMoistureClass"),
        "weatherDeclaredWet": latest_conditions.get("weatherDeclaredWet"),
        "skies": latest_conditions.get("skies"),
        "windVelMetersPerSecond": clean_float(latest_conditions.get("windVelMetersPerSecond")),
        "windDirRadians": clean_float(latest_conditions.get("windDirRadians")),
        "relativeHumidityPercent": clean_float(latest_conditions.get("relativeHumidityPercent")),
        "fogLevelPercent": clean_float(latest_conditions.get("fogLevelPercent")),
        "precipitationPercent": clean_float(latest_conditions.get("precipitationPercent")),
        "airDensityKgPerCubicMeter": clean_float(latest_conditions.get("airDensityKgPerCubicMeter")),
        "airPressurePa": clean_float(latest_conditions.get("airPressurePa")),
        "solarAltitudeRadians": clean_float(latest_conditions.get("solarAltitudeRadians")),
        "solarAzimuthRadians": clean_float(latest_conditions.get("solarAzimuthRadians")),
        "playerTireCompound": latest_conditions.get("playerTireCompound"),
        "trackWeatherType": weekend.get("TrackWeatherType"),
        "trackSkies": weekend.get("TrackSkies"),
        "sessionTrackSurfaceTempC": first_number(weekend.get("TrackSurfaceTemp")),
        "sessionTrackSurfaceTempCrewC": first_number(weekend.get("TrackSurfaceTempCrew")),
        "sessionTrackAirTempC": first_number(weekend.get("TrackAirTemp")),
        "sessionTrackAirPressure": first_number(weekend.get("TrackAirPressure")),
        "sessionTrackAirDensityKgPerCubicMeter": first_number(weekend.get("TrackAirDensity")),
        "sessionTrackWindVelMetersPerSecond": first_number(weekend.get("TrackWindVel")),
        "sessionTrackWindDirRadians": first_number(weekend.get("TrackWindDir")),
        "sessionTrackRelativeHumidityPercent": first_number(weekend.get("TrackRelativeHumidity")),
        "sessionTrackFogLevelPercent": first_number(weekend.get("TrackFogLevel")),
        "trackPrecipitationPercent": first_number(weekend.get("TrackPrecipitation")),
        "sessionTrackRubberState": session.get("SessionTrackRubberState"),
    }

    source_quality = quality(metrics)
    summary = {
        "summaryVersion": 1,
        "sourceCaptureId": capture_dir.name,
        "startedAtUtc": manifest["startedAtUtc"],
        "finishedAtUtc": manifest["finishedAtUtc"],
        "combo": combo,
        "car": car,
        "track": track,
        "session": session_identity,
        "conditions": conditions,
        "metrics": metrics,
        "quality": source_quality,
        "appVersion": None,
        "analysisSource": {
            "generatedBy": "tools/analysis/export_history_from_capture.py",
            "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "sanitized": True,
            "fuelSource": "live_local_driver_scalar_frames_only",
            "lapTimingSource": "CarIdxLastLapTime team-car array",
            "pitStopSource": "CarIdxOnPitRoad team-car array",
            "stintSource": "session-info active driver changes",
            "limitations": [
                "Direct FuelLevel/FuelUsePerHour was not available during teammate stints in this capture.",
                "Fuel-per-lap is computed from local-driver green-flag scalar telemetry only.",
                "Lap timing and pit-road counts use team-car CarIdx arrays because those remain valid while spotting.",
            ],
            "teamResult": None,
            "stints": stints,
            "pitWindows": telemetry["teamPitWindows"],
            "teamLapTimesSeconds": telemetry["teamLapTimes"],
            "weatherTimeline": telemetry["weatherTimeline"],
            "weatherTimelineTruncated": telemetry["weatherTimelineTruncated"],
        },
    }

    aggregate = {
        "aggregateVersion": 1,
        "combo": combo,
        "car": car,
        "track": track,
        "session": session_identity,
        "updatedAtUtc": manifest["finishedAtUtc"],
        "sessionCount": 1,
        "baselineSessionCount": 1 if source_quality["contributesToBaseline"] else 0,
        "fuelPerLapLiters": running_metric(metrics.get("fuelPerLapLiters") if source_quality["contributesToBaseline"] else None),
        "fuelPerHourLiters": running_metric(metrics.get("fuelPerHourLiters") if source_quality["contributesToBaseline"] else None),
        "averageLapSeconds": running_metric(metrics.get("averageLapSeconds") if source_quality["contributesToBaseline"] else None),
        "medianLapSeconds": running_metric(metrics.get("medianLapSeconds") if source_quality["contributesToBaseline"] else None),
        "pitRoadEntryCount": running_metric(metrics.get("pitRoadEntryCount") if source_quality["contributesToBaseline"] else None),
        "pitServiceCount": running_metric(metrics.get("pitServiceCount") if source_quality["contributesToBaseline"] else None),
        "analysisSource": {
            "sourceCaptureId": capture_dir.name,
            "sanitized": True,
            "fuelSource": "live_local_driver_scalar_frames_only",
            "lapTimingSource": "CarIdxLastLapTime team-car array",
            "pitStopSource": "CarIdxOnPitRoad team-car array",
        },
    }

    session_dir = output_root / "cars" / combo["carKey"] / "tracks" / combo["trackKey"] / "sessions" / combo["sessionKey"]
    summary_dir = session_dir / "summaries"
    summary_dir.mkdir(parents=True, exist_ok=True)
    summary_path = summary_dir / f"{slug(capture_dir.name)}.json"
    aggregate_path = session_dir / "aggregate.json"
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    aggregate_path.write_text(json.dumps(aggregate, indent=2) + "\n", encoding="utf-8")
    return summary_path, aggregate_path, summary, aggregate


def main() -> None:
    parser = argparse.ArgumentParser(description="Export a compact history baseline from a raw capture.")
    parser.add_argument("--capture", type=Path, default=PRIMARY_CAPTURE)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    summary_path, aggregate_path, summary, _ = build_summary(args.capture, args.output_root)
    metrics = summary["metrics"]
    print(f"Wrote {summary_path}")
    print(f"Wrote {aggregate_path}")
    fuel_per_lap = metrics.get("fuelPerLapLiters")
    fuel_per_hour = metrics.get("fuelPerHourLiters")
    valid_distance_laps = metrics.get("validDistanceLaps") or 0
    if fuel_per_lap is None or fuel_per_hour is None:
        print(
            "Fuel baseline unavailable: "
            f"{valid_distance_laps:.3f} local-driver valid laps, "
            f"confidence={summary['quality']['confidence']}"
        )
        return

    print(
        "Fuel baseline: "
        f"{fuel_per_lap:.3f} L/lap, "
        f"{fuel_per_hour:.3f} L/h, "
        f"{valid_distance_laps:.3f} local-driver valid laps"
    )


if __name__ == "__main__":
    main()
