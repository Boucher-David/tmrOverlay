#!/usr/bin/env python3
from __future__ import annotations

import argparse
import concurrent.futures as cf
import json
import math
import os
import re
import statistics
import struct
import time
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable

import yaml


SCALAR_TYPES = (str, int, float, bool, type(None))
FRAME_HEADER = struct.Struct("<qiiidi")
FILE_HEADER_BYTES = 32

WEEKEND_KEYS = [
    "TrackName",
    "TrackDisplayName",
    "TrackDisplayShortName",
    "TrackConfigName",
    "TrackID",
    "TrackLength",
    "TrackLengthOfficial",
    "TrackNumTurns",
    "TrackPitSpeedLimit",
    "TrackNumPitStalls",
    "TrackVersion",
    "EventType",
    "Category",
    "SeriesID",
    "SeasonID",
    "SessionID",
    "SubSessionID",
    "Official",
    "TeamRacing",
    "MinDrivers",
    "MaxDrivers",
    "DCRuleSet",
    "NumCarClasses",
    "NumCarTypes",
    "BuildVersion",
    "RaceFarm",
]

WEATHER_KEYS = [
    "TrackWeatherType",
    "TrackSkies",
    "TrackSurfaceTemp",
    "TrackSurfaceTempCrew",
    "TrackAirTemp",
    "TrackAirPressure",
    "TrackAirDensity",
    "TrackWindVel",
    "TrackWindDir",
    "TrackRelativeHumidity",
    "TrackFogLevel",
    "TrackPrecipitation",
    "TrackDynamicTrack",
    "WeekendOptions.WeatherType",
    "WeekendOptions.Skies",
    "WeekendOptions.WindDirection",
    "WeekendOptions.WindSpeed",
    "WeekendOptions.WeatherTemp",
    "WeekendOptions.RelativeHumidity",
    "WeekendOptions.FogLevel",
    "WeekendOptions.TimeOfDay",
    "WeekendOptions.Date",
]

DRIVER_SCALAR_KEYS = [
    "DriverCarIdx",
    "DriverUserID",
    "DriverCarFuelKgPerLtr",
    "DriverCarFuelMaxLtr",
    "DriverCarMaxFuelPct",
    "DriverCarEstLapTime",
    "DriverPitTrkPct",
    "DriverIncidentCount",
    "DriverSetupName",
    "DriverSetupIsModified",
    "DriverSetupLoadTypeName",
    "DriverSetupPassedTech",
    "DriverCarVersion",
    "DriverCarEngCylinderCount",
    "DriverCarGearNumForward",
    "DriverGearboxType",
    "DriverCarIsElectric",
]

ACTIVE_DRIVER_KEYS = [
    "CarIdx",
    "UserName",
    "UserID",
    "TeamID",
    "TeamName",
    "CarNumber",
    "CarNumberRaw",
    "CarPath",
    "CarID",
    "CarScreenName",
    "CarScreenNameShort",
    "CarClassID",
    "CarClassShortName",
    "CarClassMaxFuelPct",
    "IRating",
    "LicString",
    "CurDriverIncidentCount",
    "TeamIncidentCount",
    "IsSpectator",
]

DRIVER_ROSTER_KEYS = [
    "CarIdx",
    "UserName",
    "UserID",
    "TeamID",
    "TeamName",
    "CarNumber",
    "CarNumberRaw",
    "CarPath",
    "CarID",
    "CarScreenName",
    "CarScreenNameShort",
    "CarClassID",
    "CarClassShortName",
    "CarClassEstLapTime",
    "IRating",
    "LicString",
    "IsSpectator",
    "CurDriverIncidentCount",
    "TeamIncidentCount",
]

SETUP_KEYS = [
    "Chassis.Rear.FuelLevel",
    "TiresAero.TireType.TireType",
    "TiresAero.LeftFront.StartingPressure",
    "TiresAero.RightFront.StartingPressure",
    "TiresAero.LeftRear.StartingPressure",
    "TiresAero.RightRear.StartingPressure",
    "TiresAero.LeftFront.LastHotPressure",
    "TiresAero.RightFront.LastHotPressure",
    "TiresAero.LeftRear.LastHotPressure",
    "TiresAero.RightRear.LastHotPressure",
    "TiresAero.LeftFront.TreadRemaining",
    "TiresAero.RightFront.TreadRemaining",
    "TiresAero.LeftRear.TreadRemaining",
    "TiresAero.RightRear.TreadRemaining",
    "TiresAero.AeroBalanceCalc.WingSetting",
    "TiresAero.AeroBalanceCalc.FrontDownforce",
    "Chassis.FrontBrakesLights.BrakePads",
    "Chassis.FrontBrakesLights.EnduranceLights",
    "Chassis.FrontBrakesLights.NightLedStripColor",
    "Chassis.FrontBrakesLights.ArbSetting",
    "Chassis.FrontBrakesLights.BrakePressureBias",
    "Chassis.InCarAdjustments.AbsSetting",
    "Chassis.InCarAdjustments.TcSetting",
    "Chassis.InCarAdjustments.BrakePressureBias",
    "Chassis.Rear.RarbRate",
    "Chassis.Rear.WingAngle",
]


def canonical_paths(value: Any, path: str = "") -> set[str]:
    paths: set[str] = set()
    if isinstance(value, dict):
        if not value:
            paths.add(path or "<root>")
        for key, child in value.items():
            child_path = f"{path}.{key}" if path else str(key)
            paths.update(canonical_paths(child, child_path))
    elif isinstance(value, list):
        child_path = f"{path}[]" if path else "[]"
        if not value:
            paths.add(child_path)
        for item in value:
            paths.update(canonical_paths(item, child_path))
    else:
        paths.add(path or "<root>")
    return paths


def flat_scalars(value: Any, prefix: str = "", include_lists: bool = False) -> dict[str, Any]:
    output: dict[str, Any] = {}
    if isinstance(value, dict):
        for key, child in value.items():
            child_path = f"{prefix}.{key}" if prefix else str(key)
            output.update(flat_scalars(child, child_path, include_lists=include_lists))
    elif isinstance(value, list):
        if include_lists:
            for index, item in enumerate(value):
                output.update(flat_scalars(item, f"{prefix}[{index}]", include_lists=include_lists))
    elif isinstance(value, SCALAR_TYPES):
        output[prefix] = value
    return output


def get_path(value: Any, path: str, default: Any = None) -> Any:
    cursor = value
    for part in path.split("."):
        if not isinstance(cursor, dict) or part not in cursor:
            return default
        cursor = cursor[part]
    return cursor


def capture_dir_for(path: Path) -> Path:
    if path.parent.name == "session-info":
        return path.parent.parent
    return path.parent


def update_number(path: Path) -> int | None:
    match = re.match(r"session-(\d+)\.yaml$", path.name)
    return int(match.group(1)) if match else None


def parse_first_number(value: Any) -> float | None:
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return float(value)
    if isinstance(value, str):
        match = re.search(r"[-+]?\d+(?:\.\d+)?", value)
        if match:
            return float(match.group(0))
    return None


def compact(value: Any) -> Any:
    if isinstance(value, float):
        if math.isnan(value) or math.isinf(value):
            return str(value)
        return round(value, 6)
    if isinstance(value, dict):
        return {key: compact(child) for key, child in value.items()}
    if isinstance(value, list):
        return [compact(child) for child in value]
    return value


def compare_key(value: Any) -> str:
    return json.dumps(compact(value), sort_keys=True, default=str)


def find_driver(data: dict[str, Any], car_idx: int | None) -> dict[str, Any]:
    if car_idx is None:
        return {}
    for driver in (data.get("DriverInfo") or {}).get("Drivers") or []:
        if driver.get("CarIdx") == car_idx:
            return driver or {}
    return {}


def find_driver_from_list(drivers: list[dict[str, Any]], car_idx: int | None) -> dict[str, Any]:
    if car_idx is None:
        return {}
    for driver in drivers:
        if driver.get("CarIdx") == car_idx:
            return driver
    return {}


def find_result(session: dict[str, Any], car_idx: int | None) -> dict[str, Any] | None:
    if car_idx is None:
        return None
    for result in session.get("ResultsPositions") or []:
        if result.get("CarIdx") == car_idx:
            return result
    return None


def find_fastest(session: dict[str, Any], car_idx: int | None) -> dict[str, Any] | None:
    if car_idx is None:
        return None
    for result in session.get("ResultsFastestLap") or []:
        if result.get("CarIdx") == car_idx:
            return result
    return None


def find_qualify_result(data: dict[str, Any], car_idx: int | None) -> dict[str, Any] | None:
    if car_idx is None:
        return None
    for result in (data.get("QualifyResultsInfo") or {}).get("Results") or []:
        if result.get("CarIdx") == car_idx:
            return result
    return None


def selected(source: dict[str, Any], keys: list[str]) -> dict[str, Any]:
    return {key: source.get(key) for key in keys if key in source}


def parse_yaml_file(path: Path) -> dict[str, Any]:
    started = time.time()
    try:
        data = yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}
        if not isinstance(data, dict):
            data = {}

        capture_dir = capture_dir_for(path)
        weekend = data.get("WeekendInfo") or {}
        session_info = data.get("SessionInfo") or {}
        driver_info = data.get("DriverInfo") or {}
        car_setup = data.get("CarSetup") or {}
        driver_scalars_all = {
            key: value
            for key, value in driver_info.items()
            if key not in {"Drivers", "DriverTires"}
        }
        driver_car_idx = driver_scalars_all.get("DriverCarIdx")
        active_driver = find_driver(data, driver_car_idx)
        drivers = [
            selected(driver, DRIVER_ROSTER_KEYS)
            for driver in driver_info.get("Drivers") or []
            if isinstance(driver, dict)
        ]
        flat_weekend = flat_scalars(weekend)
        flat_setup = flat_scalars(car_setup)
        sessions = []
        for session in session_info.get("Sessions") or []:
            if not isinstance(session, dict):
                continue
            sessions.append(
                {
                    "SessionNum": session.get("SessionNum"),
                    "SessionType": session.get("SessionType"),
                    "SessionName": session.get("SessionName"),
                    "SessionLaps": session.get("SessionLaps"),
                    "SessionTime": session.get("SessionTime"),
                    "SessionTrackRubberState": session.get("SessionTrackRubberState"),
                    "SessionRunGroupsUsed": session.get("SessionRunGroupsUsed"),
                    "SessionSkipped": session.get("SessionSkipped"),
                    "ResultsOfficial": session.get("ResultsOfficial"),
                    "ResultsLapsComplete": session.get("ResultsLapsComplete"),
                    "ResultsAverageLapTime": session.get("ResultsAverageLapTime"),
                    "ResultsNumCautionFlags": session.get("ResultsNumCautionFlags"),
                    "ResultsNumCautionLaps": session.get("ResultsNumCautionLaps"),
                    "ResultsNumLeadChanges": session.get("ResultsNumLeadChanges"),
                    "ResultsPositionCount": len(session.get("ResultsPositions") or []),
                    "ResultsFastestLapCount": len(session.get("ResultsFastestLap") or []),
                    "TeamResultForCurrentDriverCarIdx": find_result(session, driver_car_idx),
                    "TeamFastestForCurrentDriverCarIdx": find_fastest(session, driver_car_idx),
                    "ResultsPositions": session.get("ResultsPositions") or [],
                    "ResultsFastestLap": session.get("ResultsFastestLap") or [],
                }
            )

        return {
            "path": str(path),
            "captureId": capture_dir.name,
            "isDuplicateImport": "_duplicate-imports" in path.parts,
            "isLatest": path.name == "latest-session.yaml",
            "update": update_number(path),
            "fileBytes": path.stat().st_size,
            "parseSeconds": round(time.time() - started, 4),
            "topLevelKeys": sorted(data.keys()),
            "canonicalPaths": sorted(canonical_paths(data)),
            "weekend": selected(weekend, WEEKEND_KEYS),
            "weather": {key: get_path(weekend, key) for key in WEATHER_KEYS if get_path(weekend, key) is not None},
            "weekendOptions": weekend.get("WeekendOptions") or {},
            "flatWeekend": flat_weekend,
            "currentSessionNum": session_info.get("CurrentSessionNum"),
            "sessions": sessions,
            "qualifyResultForCurrentDriverCarIdx": find_qualify_result(data, driver_car_idx),
            "qualifyResultCount": len((data.get("QualifyResultsInfo") or {}).get("Results") or []),
            "driverScalars": selected(driver_scalars_all, DRIVER_SCALAR_KEYS),
            "driverScalarsAll": driver_scalars_all,
            "driverTires": driver_info.get("DriverTires") or [],
            "driverCount": len(driver_info.get("Drivers") or []),
            "drivers": drivers,
            "activeDriver": selected(active_driver, ACTIVE_DRIVER_KEYS),
            "setup": {key: flat_setup.get(key) for key in SETUP_KEYS if key in flat_setup},
            "flatSetup": flat_setup,
            "splitSectors": (data.get("SplitTimeInfo") or {}).get("Sectors") or [],
            "radioCount": len((data.get("RadioInfo") or {}).get("Radios") or []),
            "cameraGroupCount": len((data.get("CameraInfo") or {}).get("Groups") or []),
        }
    except Exception as exc:
        return {
            "path": str(path),
            "error": repr(exc),
            "parseSeconds": round(time.time() - started, 4),
        }


def read_manifest(capture_dir: Path) -> dict[str, Any]:
    manifest_path = capture_dir / "capture-manifest.json"
    if not manifest_path.exists():
        return {}
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except Exception as exc:
        return {"error": repr(exc)}
    return {
        "captureId": manifest.get("captureId"),
        "startedAtUtc": manifest.get("startedAtUtc"),
        "finishedAtUtc": manifest.get("finishedAtUtc"),
        "frameCount": manifest.get("frameCount"),
        "droppedFrameCount": manifest.get("droppedFrameCount"),
        "sessionInfoSnapshotCount": manifest.get("sessionInfoSnapshotCount"),
        "tickRate": manifest.get("tickRate"),
        "bufferLength": manifest.get("bufferLength"),
        "variableCount": manifest.get("variableCount"),
    }


def read_update_timing(capture_dir: Path, wanted_updates: set[int]) -> dict[int, dict[str, Any]]:
    telemetry_path = capture_dir / "telemetry.bin"
    if not telemetry_path.exists() or not wanted_updates:
        return {}
    output: dict[int, dict[str, Any]] = {}
    with telemetry_path.open("rb") as stream:
        if len(stream.read(FILE_HEADER_BYTES)) != FILE_HEADER_BYTES:
            return output
        while True:
            header = stream.read(FRAME_HEADER.size)
            if not header:
                break
            if len(header) != FRAME_HEADER.size:
                break
            captured_ms, frame_index, session_tick, update, session_time, payload_len = FRAME_HEADER.unpack(header)
            if update in wanted_updates:
                entry = output.setdefault(
                    update,
                    {
                        "firstCapturedUnixMs": captured_ms,
                        "firstCapturedUtc": iso_from_ms(captured_ms),
                        "firstFrameIndex": frame_index,
                        "firstSessionTick": session_tick,
                        "firstSessionTime": session_time,
                        "lastCapturedUnixMs": captured_ms,
                        "lastCapturedUtc": iso_from_ms(captured_ms),
                        "lastFrameIndex": frame_index,
                        "lastSessionTick": session_tick,
                        "lastSessionTime": session_time,
                        "frameCount": 0,
                    },
                )
                entry["lastCapturedUnixMs"] = captured_ms
                entry["lastCapturedUtc"] = iso_from_ms(captured_ms)
                entry["lastFrameIndex"] = frame_index
                entry["lastSessionTick"] = session_tick
                entry["lastSessionTime"] = session_time
                entry["frameCount"] += 1
            stream.seek(payload_len, os.SEEK_CUR)
    return output


def iso_from_ms(value: int | None) -> str | None:
    if value is None:
        return None
    return datetime.fromtimestamp(value / 1000, tz=timezone.utc).isoformat().replace("+00:00", "Z")


def session_time_label(seconds: float | None) -> str:
    if seconds is None:
        return "n/a"
    total = int(round(seconds))
    hours, remainder = divmod(total, 3600)
    minutes, second = divmod(remainder, 60)
    if hours:
        return f"{hours}:{minutes:02d}:{second:02d}"
    return f"{minutes}:{second:02d}"


def change_list(rows: list[dict[str, Any]], getter: Callable[[dict[str, Any]], Any]) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    sentinel = object()
    previous = sentinel
    previous_key = ""
    for row in rows:
        value = compact(getter(row))
        key = compare_key(value)
        if previous is sentinel or key != previous_key:
            output.append(
                {
                    "path": row["path"],
                    "update": row.get("update"),
                    "isLatest": row.get("isLatest"),
                    "currentSessionNum": row.get("currentSessionNum"),
                    "value": value,
                }
            )
            previous = value
            previous_key = key
    return output


def changed_fields(rows: list[dict[str, Any]], getter: Callable[[dict[str, Any]], dict[str, Any]]) -> dict[str, list[dict[str, Any]]]:
    keys = sorted({key for row in rows for key in getter(row).keys()})
    output: dict[str, list[dict[str, Any]]] = {}
    for key in keys:
        changes = change_list(rows, lambda row, field=key: getter(row).get(field))
        if len(changes) > 1:
            output[key] = changes
    return output


def attach_timing(changes: list[dict[str, Any]], timing: dict[int, dict[str, Any]]) -> None:
    for change in changes:
        update = change.get("update")
        if update in timing:
            change["timing"] = timing[update]


def attach_timing_nested(value: Any, timing: dict[int, dict[str, Any]]) -> None:
    if isinstance(value, list):
        if all(isinstance(item, dict) and "update" in item for item in value):
            attach_timing(value, timing)
            return

        for child in value:
            attach_timing_nested(child, timing)
    elif isinstance(value, dict):
        for child in value.values():
            attach_timing_nested(child, timing)


def result_for_car(row: dict[str, Any], session_num: int | None, car_idx: int | None) -> dict[str, Any] | None:
    for session in row.get("sessions") or []:
        if session.get("SessionNum") == session_num:
            return find_result(session, car_idx)
    return None


def fastest_for_car(row: dict[str, Any], session_num: int | None, car_idx: int | None) -> dict[str, Any] | None:
    for session in row.get("sessions") or []:
        if session.get("SessionNum") == session_num:
            return find_fastest(session, car_idx)
    return None


def final_standings(latest: dict[str, Any], session_num: int | None, car_idx: int | None) -> list[dict[str, Any]]:
    drivers = latest.get("drivers") or []
    sessions = latest.get("sessions") or []
    session = next((item for item in sessions if item.get("SessionNum") == session_num), None)
    if not session:
        return []
    rows = []
    for result in session.get("ResultsPositions") or []:
        driver = find_driver_from_list(drivers, result.get("CarIdx"))
        row = dict(result)
        row["UserName"] = driver.get("UserName")
        row["TeamName"] = driver.get("TeamName")
        row["CarNumber"] = driver.get("CarNumber")
        row["CarScreenNameShort"] = driver.get("CarScreenNameShort")
        row["CarClassID"] = driver.get("CarClassID")
        row["CarClassShortName"] = driver.get("CarClassShortName")
        row["IsTeamCar"] = result.get("CarIdx") == car_idx
        rows.append(row)
    return sorted(rows, key=lambda item: (item.get("Position") is None, item.get("Position") or 9999))


def summarize_roster(latest: dict[str, Any]) -> dict[str, Any]:
    drivers = latest.get("drivers") or []
    class_counts: Counter[str] = Counter()
    car_counts: Counter[str] = Counter()
    teams: set[str] = set()
    users: set[str] = set()
    for driver in drivers:
        if driver.get("IsSpectator"):
            continue
        class_label = driver.get("CarClassShortName") or f"class-{driver.get('CarClassID')}"
        car_label = driver.get("CarScreenNameShort") or driver.get("CarScreenName") or driver.get("CarPath") or "unknown"
        class_counts[class_label] += 1
        car_counts[car_label] += 1
        if driver.get("TeamName"):
            teams.add(str(driver["TeamName"]))
        if driver.get("UserName"):
            users.add(str(driver["UserName"]))
    return {
        "driverRows": len(drivers),
        "nonSpectatorRows": sum(1 for driver in drivers if not driver.get("IsSpectator")),
        "uniqueTeams": len(teams),
        "uniqueUsers": len(users),
        "classCounts": dict(class_counts.most_common()),
        "carCounts": dict(car_counts.most_common()),
    }


def build_stints(rows: list[dict[str, Any]], primary_session_num: int, primary_car_idx: int | None) -> list[dict[str, Any]]:
    race_rows = [row for row in rows if row.get("currentSessionNum") == primary_session_num]
    driver_changes = change_list(
        race_rows,
        lambda row: (
            row.get("currentSessionNum"),
            row.get("driverScalars", {}).get("DriverCarIdx"),
            row.get("activeDriver", {}).get("UserName"),
            row.get("activeDriver", {}).get("UserID"),
        ),
    )
    starts = [change for change in driver_changes if change["value"][1] == primary_car_idx]
    output = []
    for index, start in enumerate(starts):
        start_update = start["update"]
        next_update = starts[index + 1]["update"] if index + 1 < len(starts) else None
        stint_rows = [
            row
            for row in race_rows
            if row.get("update") is not None
            and start_update is not None
            and row["update"] >= start_update
            and (next_update is None or row["update"] < next_update)
        ]
        if not stint_rows:
            continue
        first = stint_rows[0]
        last = stint_rows[-1]
        first_result = result_for_car(first, primary_session_num, primary_car_idx)
        last_result = result_for_car(last, primary_session_num, primary_car_idx)
        first_timing = first.get("timing") or {}
        last_timing = last.get("timing") or {}
        start_ms = first_timing.get("firstCapturedUnixMs")
        end_ms = last_timing.get("lastCapturedUnixMs")
        duration_s = (end_ms - start_ms) / 1000 if isinstance(start_ms, int) and isinstance(end_ms, int) else None
        output.append(
            {
                "driverName": first.get("activeDriver", {}).get("UserName"),
                "driverUserId": first.get("activeDriver", {}).get("UserID"),
                "startUpdate": first.get("update"),
                "endUpdate": last.get("update"),
                "startRaceTime": first_timing.get("firstSessionTime"),
                "endRaceTime": last_timing.get("lastSessionTime"),
                "durationSeconds": duration_s,
                "startLapsComplete": (first_result or {}).get("LapsComplete"),
                "endLapsComplete": (last_result or {}).get("LapsComplete"),
                "startClassPosition": (first_result or {}).get("ClassPosition"),
                "endClassPosition": (last_result or {}).get("ClassPosition"),
                "startOverallPosition": (first_result or {}).get("Position"),
                "endOverallPosition": (last_result or {}).get("Position"),
                "startCurrentDriverIncidents": first.get("activeDriver", {}).get("CurDriverIncidentCount"),
                "endCurrentDriverIncidents": last.get("activeDriver", {}).get("CurDriverIncidentCount"),
                "startTeamIncidents": first.get("activeDriver", {}).get("TeamIncidentCount"),
                "endTeamIncidents": last.get("activeDriver", {}).get("TeamIncidentCount"),
                "fastestLap": (last_result or {}).get("FastestLap"),
                "fastestTime": (last_result or {}).get("FastestTime"),
                "lastTime": (last_result or {}).get("LastTime"),
            }
        )
    return output


def update_cadence(rows: list[dict[str, Any]]) -> dict[str, Any]:
    timed = [
        row
        for row in rows
        if row.get("timing") and isinstance(row["timing"].get("firstCapturedUnixMs"), int)
    ]
    if len(timed) < 2:
        return {}
    deltas = [
        (timed[index]["timing"]["firstCapturedUnixMs"] - timed[index - 1]["timing"]["firstCapturedUnixMs"]) / 1000
        for index in range(1, len(timed))
    ]
    return {
        "timedSnapshots": len(timed),
        "medianSecondsBetweenSnapshots": round(statistics.median(deltas), 3),
        "meanSecondsBetweenSnapshots": round(statistics.mean(deltas), 3),
        "minSecondsBetweenSnapshots": round(min(deltas), 3),
        "maxSecondsBetweenSnapshots": round(max(deltas), 3),
    }


def summarize_capture(capture_dir: Path, rows: list[dict[str, Any]]) -> dict[str, Any]:
    rows = sorted(rows, key=lambda row: (row.get("isLatest", False), row.get("update") if row.get("update") is not None else 10**9, row["path"]))
    session_rows = [row for row in rows if not row.get("isLatest")]
    latest = next((row for row in rows if row.get("isLatest")), rows[-1])
    updates = {row["update"] for row in session_rows if row.get("update") is not None}
    timing = read_update_timing(capture_dir, updates)
    for row in rows:
        update = row.get("update")
        if update in timing:
            row["timing"] = timing[update]

    primary_car_idx = latest.get("driverScalars", {}).get("DriverCarIdx")
    primary_session_num = latest.get("currentSessionNum")
    update_values = sorted(updates)
    top_level_keys = sorted({key for row in rows for key in row.get("topLevelKeys", [])})
    all_paths = sorted({path for row in rows for path in row.get("canonicalPaths", [])})
    active_driver_changes = change_list(
        session_rows,
        lambda row: (
            row.get("currentSessionNum"),
            row.get("driverScalars", {}).get("DriverCarIdx"),
            row.get("activeDriver", {}).get("UserName"),
            row.get("activeDriver", {}).get("UserID"),
            row.get("driverScalars", {}).get("DriverUserID"),
        ),
    )
    current_session_changes = change_list(session_rows, lambda row: row.get("currentSessionNum"))
    weather_changes = changed_fields(session_rows, lambda row: row.get("weather") or {})
    driver_scalar_changes = changed_fields(session_rows, lambda row: row.get("driverScalars") or {})
    active_driver_field_changes = changed_fields(session_rows, lambda row: row.get("activeDriver") or {})
    setup_changes = changed_fields(session_rows, lambda row: row.get("setup") or {})
    qualify_changes = change_list(session_rows, lambda row: row.get("qualifyResultForCurrentDriverCarIdx"))

    session_nums = sorted(
        {
            session.get("SessionNum")
            for row in rows
            for session in row.get("sessions", [])
            if session.get("SessionNum") is not None
        }
    )
    team_result_changes = {
        str(session_num): change_list(
            session_rows,
            lambda row, sn=session_num: result_for_car(row, sn, primary_car_idx),
        )
        for session_num in session_nums
    }
    team_fastest_changes = {
        str(session_num): change_list(
            session_rows,
            lambda row, sn=session_num: fastest_for_car(row, sn, primary_car_idx),
        )
        for session_num in session_nums
    }

    report = {
        "captureId": capture_dir.name,
        "duplicateImport": any(row.get("isDuplicateImport") for row in rows),
        "fileCount": len(rows),
        "sessionInfoFileCount": len(session_rows),
        "updateRange": [update_values[0], update_values[-1]] if update_values else None,
        "manifest": read_manifest(capture_dir),
        "topLevelKeys": top_level_keys,
        "canonicalPathCount": len(all_paths),
        "latest": {
            "path": latest["path"],
            "weekend": latest.get("weekend"),
            "weather": latest.get("weather"),
            "currentSessionNum": latest.get("currentSessionNum"),
            "sessions": [
                {key: value for key, value in session.items() if key not in {"ResultsPositions", "ResultsFastestLap"}}
                for session in latest.get("sessions", [])
            ],
            "driverScalars": latest.get("driverScalars"),
            "activeDriver": latest.get("activeDriver"),
            "driverTires": latest.get("driverTires"),
            "setup": latest.get("setup"),
            "splitSectors": latest.get("splitSectors"),
            "rosterSummary": summarize_roster(latest),
        },
        "currentSessionChanges": current_session_changes,
        "activeDriverChanges": active_driver_changes,
        "activeDriverFieldChanges": {key: value for key, value in active_driver_field_changes.items() if len(value) > 1},
        "driverScalarChanges": {key: value for key, value in driver_scalar_changes.items() if len(value) > 1},
        "weatherChanges": {key: value for key, value in weather_changes.items() if len(value) > 1},
        "setupChanges": {key: value for key, value in setup_changes.items() if len(value) > 1},
        "qualifyResultChanges": qualify_changes if len(qualify_changes) > 1 else [],
        "teamResultChangesBySession": {key: value for key, value in team_result_changes.items() if len(value) > 1},
        "teamFastestChangesBySession": {key: value for key, value in team_fastest_changes.items() if len(value) > 1},
        "finalStandings": final_standings(latest, primary_session_num, primary_car_idx),
        "stints": build_stints(rows, primary_session_num, primary_car_idx) if primary_session_num is not None else [],
        "snapshotCadence": update_cadence(session_rows),
    }
    attach_timing_nested(report, timing)
    return report


def md_value(value: Any) -> str:
    if value is None:
        return "n/a"
    if isinstance(value, float):
        return f"{value:.3f}".rstrip("0").rstrip(".")
    return str(value)


def change_time(change: dict[str, Any]) -> str:
    timing = change.get("timing") or {}
    return session_time_label(timing.get("firstSessionTime"))


def render_markdown(report: dict[str, Any], output_json: Path) -> str:
    lines: list[str] = []
    lines.append("# YAML Forensics Report")
    lines.append("")
    lines.append(f"- Parsed YAML files: {report['parsedFileCount']}")
    lines.append(f"- Parse errors: {report['parseErrorCount']}")
    lines.append(f"- Distinct canonical scalar paths: {report['distinctCanonicalPathCount']}")
    lines.append(f"- JSON details: `{output_json}`")
    lines.append("")

    lines.append("## Capture Inventory")
    for capture_id, capture in report["captures"].items():
        latest = capture["latest"]
        weekend = latest["weekend"]
        active = latest["activeDriver"]
        manifest = capture.get("manifest") or {}
        lines.append(
            "- "
            f"`{capture_id}`: yaml={capture['fileCount']} files, "
            f"session-info={capture['sessionInfoFileCount']}, "
            f"frames={manifest.get('frameCount', 'n/a')}, "
            f"dropped={manifest.get('droppedFrameCount', 'n/a')}, "
            f"event={weekend.get('EventType')}, "
            f"track={weekend.get('TrackDisplayName')} / {weekend.get('TrackName')}, "
            f"active={active.get('UserName')} #{active.get('CarNumber')} {active.get('CarScreenNameShort')}"
        )
    lines.append("")

    lines.append("## YAML Shape")
    lines.append("- Top-level sections found: " + ", ".join(report["topLevelSections"]))
    lines.append("- Always-present scalar paths are mostly static metadata: camera groups, driver/car descriptors, radio metadata, session descriptors, split sectors, and weekend/track settings.")
    optional = report["optionalPathSummary"]
    lines.append(
        "- Most important optional sections: "
        + ", ".join(f"`{item['path']}` in {item['count']} files" for item in optional[:10])
    )
    lines.append("")

    race = report["captures"].get("capture-20260426-130334-932")
    if race:
        latest = race["latest"]
        weekend = latest["weekend"]
        active = latest["activeDriver"]
        setup = latest["setup"]
        cadence = race.get("snapshotCadence") or {}
        lines.append("## 4-Hour Race")
        lines.append(f"- Track/session: {weekend.get('TrackDisplayName')} (`{weekend.get('TrackName')}`), {weekend.get('EventType')}, SubSessionID `{weekend.get('SubSessionID')}`.")
        lines.append(
            f"- Capture: {race['manifest'].get('frameCount')} frames at {race['manifest'].get('tickRate')} Hz, "
            f"{race['manifest'].get('sessionInfoSnapshotCount')} session-info snapshots, "
            f"{race['manifest'].get('droppedFrameCount')} dropped frames."
        )
        if cadence:
            lines.append(
                "- YAML snapshot cadence: "
                f"median {cadence.get('medianSecondsBetweenSnapshots')}s, "
                f"mean {cadence.get('meanSecondsBetweenSnapshots')}s, "
                f"range {cadence.get('minSecondsBetweenSnapshots')}s-{cadence.get('maxSecondsBetweenSnapshots')}s between SessionInfoUpdate changes."
            )
        lines.append(
            f"- Team car metadata: carIdx={active.get('CarIdx')}, car #{active.get('CarNumber')}, "
            f"{active.get('CarScreenNameShort')}, max fuel {latest['driverScalars'].get('DriverCarFuelMaxLtr')} L, "
            f"fuel density {latest['driverScalars'].get('DriverCarFuelKgPerLtr')} kg/L."
        )
        lines.append(
            f"- Setup fuel in YAML: `{setup.get('Chassis.Rear.FuelLevel')}`. This is setup fuel, not live fuel burn; it did not change with consumption."
        )
        lines.append("- Sessions:")
        for session in latest["sessions"]:
            lines.append(
                f"  - {session.get('SessionNum')}: {session.get('SessionType')} / {session.get('SessionName')}, "
                f"time={session.get('SessionTime')}, laps={session.get('SessionLaps')}, "
                f"results={session.get('ResultsPositionCount')}"
            )
        lines.append("- Active driver transitions:")
        for change in race["activeDriverChanges"]:
            value = change["value"]
            lines.append(
                f"  - update {change.get('update')} at session {value[0]} {change_time(change)}: "
                f"carIdx={value[1]}, active={value[2]}, activeUserId={value[3]}, DriverUserID={value[4]}"
            )
        lines.append("- Race stints inferred from YAML active-driver changes:")
        for stint in race.get("stints") or []:
            laps = ""
            if stint.get("startLapsComplete") is not None and stint.get("endLapsComplete") is not None:
                laps = f", lapsComplete {stint['startLapsComplete']}->{stint['endLapsComplete']}"
            duration = stint.get("durationSeconds")
            duration_text = session_time_label(duration) if duration is not None else "n/a"
            lines.append(
                f"  - {stint.get('driverName')}: {session_time_label(stint.get('startRaceTime'))}-"
                f"{session_time_label(stint.get('endRaceTime'))}, duration {duration_text}{laps}, "
                f"incidents {stint.get('startCurrentDriverIncidents')}->{stint.get('endCurrentDriverIncidents')}, "
                f"team incidents {stint.get('startTeamIncidents')}->{stint.get('endTeamIncidents')}"
            )
        lines.append("")

        lines.append("## Race Results And Roster")
        final_team = next((row for row in race.get("finalStandings", []) if row.get("IsTeamCar")), None)
        if final_team:
            lines.append(
                f"- Team result: P{final_team.get('Position')} overall, P{final_team.get('ClassPosition')} class, "
                f"{final_team.get('LapsComplete')} completed laps, best lap {md_value(final_team.get('FastestTime'))}s, "
                f"last lap {md_value(final_team.get('LastTime'))}s."
            )
        roster = latest["rosterSummary"]
        lines.append(
            f"- Roster: {roster.get('nonSpectatorRows')} non-spectator rows, {roster.get('uniqueTeams')} teams/users represented as driver rows."
        )
        lines.append(
            "- Class mix: "
            + ", ".join(f"{name}={count}" for name, count in list(roster.get("classCounts", {}).items())[:8])
        )
        lines.append("- Top 10 final race standings:")
        for row in race.get("finalStandings", [])[:10]:
            marker = " (team car)" if row.get("IsTeamCar") else ""
            lines.append(
                f"  - P{row.get('Position')} / C{row.get('ClassPosition')}: "
                f"#{row.get('CarNumber')} {row.get('TeamName') or row.get('UserName')} "
                f"{row.get('CarScreenNameShort')}, laps={row.get('LapsComplete')}{marker}"
            )
        lines.append("")

        lines.append("## Changing YAML Fields")
        field_groups = [
            ("Driver scalars", race.get("driverScalarChanges") or {}),
            ("Active driver fields", race.get("activeDriverFieldChanges") or {}),
            ("Setup fields", race.get("setupChanges") or {}),
            ("Weather fields", race.get("weatherChanges") or {}),
        ]
        for label, changes in field_groups:
            if changes:
                lines.append(f"- {label}: " + ", ".join(f"`{key}` ({len(value)} changes)" for key, value in changes.items()))
            else:
                lines.append(f"- {label}: no meaningful changes in captured YAML snapshots.")
        result_counts = {
            key: len(value)
            for key, value in (race.get("teamResultChangesBySession") or {}).items()
            if value
        }
        lines.append(
            "- Team result objects changed by session: "
            + ", ".join(f"session {key}={value}" for key, value in result_counts.items())
        )
        lines.append("")

    lines.append("## Product Implications")
    lines.append("- YAML is excellent for event identity, track/car combo keys, roster, active driver handoff, setup, official session structure, result snapshots, and incident counts.")
    lines.append("- YAML is not a live fuel source. `CarSetup.Chassis.Rear.FuelLevel` is setup fuel and does not represent fuel remaining during the race.")
    lines.append("- For historical fuel modeling, use YAML to label the session and segment teammate stints, then use `telemetry.bin` scalar fuel only when the local driver owns the car. During teammate stints, treat fuel as inferred or unavailable unless another iRacing source exposes it.")
    lines.append("- Store confidence/source flags per historical metric: `live_local_scalar`, `yaml_result_snapshot`, `car_idx_timing`, `setup_static`, or `inferred`.")
    lines.append("")
    return "\n".join(lines) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description="Analyze captured iRacing session YAML snapshots.")
    parser.add_argument("--captures-root", default="captures")
    parser.add_argument("--workers", type=int, default=min(8, os.cpu_count() or 4))
    args = parser.parse_args()

    root = Path(args.captures_root)
    output_dir = root / "_analysis"
    output_dir.mkdir(parents=True, exist_ok=True)
    output_json = output_dir / "yaml-forensics.json"
    output_md = output_dir / "yaml-forensics.md"
    yaml_files = sorted(path for path in root.rglob("*.yaml") if path.is_file())

    started = time.time()
    with cf.ProcessPoolExecutor(max_workers=args.workers) as pool:
        parsed = list(pool.map(parse_yaml_file, yaml_files, chunksize=16))

    errors = [row for row in parsed if "error" in row]
    valid = [row for row in parsed if "error" not in row]
    by_capture: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in valid:
        by_capture[row["captureId"]].append(row)

    path_counter: Counter[str] = Counter()
    for row in valid:
        path_counter.update(row["canonicalPaths"])

    captures = {
        capture_id: summarize_capture(capture_dir_for(Path(rows[0]["path"])), rows)
        for capture_id, rows in sorted(by_capture.items())
    }
    top_level_sections = sorted({section for row in valid for section in row.get("topLevelKeys", [])})
    optional_paths = [
        {"path": path, "count": count, "missing": len(valid) - count}
        for path, count in path_counter.most_common()
        if count != len(valid)
    ]
    report = {
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "elapsedSeconds": round(time.time() - started, 3),
        "parsedFileCount": len(valid),
        "parseErrorCount": len(errors),
        "parseErrors": errors,
        "distinctCanonicalPathCount": len(path_counter),
        "topLevelSections": top_level_sections,
        "canonicalPathCounts": dict(path_counter.most_common()),
        "optionalPathSummary": optional_paths[:80],
        "captures": captures,
    }

    output_json.write_text(json.dumps(report, indent=2, default=str), encoding="utf-8")
    output_md.write_text(render_markdown(report, output_json), encoding="utf-8")
    print(f"Parsed {len(valid)} YAML files with {len(errors)} errors in {report['elapsedSeconds']}s")
    print(f"Wrote {output_json}")
    print(f"Wrote {output_md}")


if __name__ == "__main__":
    main()
