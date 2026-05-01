#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import math
import os
import platform
import subprocess
import struct
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import yaml


FILE_HEADER = struct.Struct("<8siiiiq")
FRAME_HEADER = struct.Struct("<qiiidi")
FILE_HEADER_BYTES = 32
DEFAULT_AUTO_SAMPLE_TARGET = 20_000
IRACING_SIM_PROCESS_NAMES = {
    "iracingsim64dx11.exe",
    "iracingsim64.exe",
    "iracingsim.exe",
}

NUMERIC_TYPES = {"irInt", "irFloat", "irDouble", "irBitField"}
SCALAR_TIMELINE_TYPES = {"irBool", "irInt", "irBitField"}

WEATHER_TERMS = [
    "weather",
    "rain",
    "precip",
    "wet",
    "moisture",
    "skies",
    "sky",
    "cloud",
    "fog",
    "humidity",
    "wind",
    "airtemp",
    "tracktemp",
    "solar",
    "radar",
]

RADAR_TERMS = ["radar", "rain", "precip", "wet", "moisture", "cloud"]

CATEGORY_TERMS = {
    "weather": WEATHER_TERMS,
    "session": ["session", "flag", "pace", "caution"],
    "timing": ["lap", "time", "position", "distance", "estimated", "f2"],
    "cars": ["caridx", "player", "driver", "team", "class"],
    "pit": ["pit", "service", "repair", "tire", "fuel"],
    "controls": ["throttle", "brake", "clutch", "steering", "gear", "input"],
    "vehicle": ["rpm", "speed", "accel", "yaw", "pitch", "roll", "velocity"],
    "tires": ["tire", "tyre", "wheel"],
    "damage": ["damage", "repair"],
    "overlay-useful": ["carleft", "tracksurface", "lapdist", "classposition", "f2time", "esttime"],
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def clean_float(value: float, digits: int = 6) -> float | None:
    if math.isnan(value) or math.isinf(value):
        return None
    return round(float(value), digits)


def compact_value(value: Any) -> Any:
    if isinstance(value, float):
        return clean_float(value)
    return value


def compare_value(value: Any) -> Any:
    if isinstance(value, float):
        cleaned = clean_float(value)
        return None if cleaned is None else round(cleaned, 5)
    return value


def is_numeric(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def is_finite_numeric(value: Any) -> bool:
    return is_numeric(value) and not math.isnan(float(value)) and not math.isinf(float(value))


def is_non_default(value: Any) -> bool:
    if value is None:
        return False
    if isinstance(value, bool):
        return value
    if is_finite_numeric(value):
        return abs(float(value)) > 0.000001
    return True


def contains_any(value: str | None, terms: list[str]) -> bool:
    if not value:
        return False
    lowered = value.lower()
    return any(term.lower() in lowered for term in terms)


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def load_yaml(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return yaml.safe_load(path.read_text(encoding="utf-8-sig")) or {}


def running_iracing_sim_processes() -> list[str]:
    if platform.system().lower() != "windows":
        return []
    try:
        result = subprocess.run(
            ["tasklist", "/fo", "csv", "/nh"],
            check=False,
            capture_output=True,
            text=True,
        )
    except OSError:
        return []
    if result.returncode != 0:
        return []

    processes: set[str] = set()
    for row in csv.reader(result.stdout.splitlines()):
        if not row:
            continue
        image_name = row[0].strip()
        if image_name.lower() in IRACING_SIM_PROCESS_NAMES:
            processes.add(image_name)
    return sorted(processes, key=str.lower)


def read_scalar(payload: bytes, variable: dict[str, Any], index: int = 0) -> Any:
    byte_size = int(variable.get("byteSize") or 0)
    offset = int(variable["offset"]) + (index * byte_size)
    if byte_size <= 0 or offset < 0 or offset + byte_size > len(payload):
        return None

    type_name = variable.get("typeName")
    if type_name == "irBool":
        return payload[offset] != 0
    if type_name == "irInt":
        return struct.unpack_from("<i", payload, offset)[0]
    if type_name == "irFloat":
        return struct.unpack_from("<f", payload, offset)[0]
    if type_name == "irDouble":
        return struct.unpack_from("<d", payload, offset)[0]
    if type_name == "irBitField":
        return struct.unpack_from("<I", payload, offset)[0]
    return None


def field_search_text(variable: dict[str, Any]) -> str:
    return " ".join(
        str(variable.get(key) or "")
        for key in ("name", "unit", "description")
    ).lower()


def infer_categories(variable: dict[str, Any]) -> list[str]:
    text = field_search_text(variable)
    categories = [
        category
        for category, terms in CATEGORY_TERMS.items()
        if contains_any(text, terms)
    ]
    if not categories:
        name = str(variable.get("name") or "")
        if name.startswith("CarIdx"):
            categories.append("cars")
        elif name.startswith(("dc", "dp")):
            categories.append("controls")
    return categories or ["uncategorized"]


class RunningStats:
    def __init__(self) -> None:
        self.count = 0
        self.total = 0.0
        self.minimum: float | None = None
        self.maximum: float | None = None

    def add(self, value: Any) -> None:
        if not is_finite_numeric(value):
            return
        number = float(value)
        self.count += 1
        self.total += number
        self.minimum = number if self.minimum is None else min(self.minimum, number)
        self.maximum = number if self.maximum is None else max(self.maximum, number)

    def to_json(self) -> dict[str, Any]:
        return {
            "finiteValueCount": self.count,
            "minimum": clean_float(self.minimum) if self.minimum is not None else None,
            "maximum": clean_float(self.maximum) if self.maximum is not None else None,
            "mean": clean_float(self.total / self.count) if self.count else None,
        }


class IndexStats:
    def __init__(self) -> None:
        self.non_default_frames = 0
        self.change_count = 0
        self.has_first_value = False
        self.first_value: Any = None
        self.last_value: Any = None
        self.previous_value: Any = None
        self.stats = RunningStats()

    def add(self, value: Any) -> None:
        if not self.has_first_value:
            self.has_first_value = True
            self.first_value = compact_value(value)
        current = compare_value(value)
        if self.previous_value is not None and current != self.previous_value:
            self.change_count += 1
        self.previous_value = current
        self.last_value = compact_value(value)
        if is_non_default(value):
            self.non_default_frames += 1
        self.stats.add(value)

    def to_json(self, index: int) -> dict[str, Any]:
        return {
            "index": index,
            "nonDefaultFrameCount": self.non_default_frames,
            "changeCount": self.change_count,
            "firstValue": self.first_value,
            "lastValue": self.last_value,
            **self.stats.to_json(),
        }


class FieldStats:
    def __init__(self, variable: dict[str, Any], max_distinct: int, max_timeline_events: int) -> None:
        self.variable = variable
        self.name = str(variable["name"])
        self.type_name = str(variable.get("typeName") or "unknown")
        self.count = int(variable.get("count") or 1)
        self.unit = variable.get("unit") or ""
        self.description = variable.get("description") or ""
        self.categories = infer_categories(variable)
        self.max_distinct = max_distinct
        self.max_timeline_events = max_timeline_events
        self.frame_count = 0
        self.value_count = 0
        self.non_default_frame_count = 0
        self.change_count = 0
        self.has_first_value = False
        self.first_value: Any = None
        self.last_value: Any = None
        self.previous_value: Any = None
        self.stats = RunningStats()
        self.distinct = Counter()
        self.distinct_overflow = False
        self.index_stats: dict[int, IndexStats] = {}
        self.timeline: list[dict[str, Any]] = []
        self.timeline_truncated = False

    @property
    def is_scalar(self) -> bool:
        return self.count == 1

    @property
    def timeline_enabled(self) -> bool:
        return self.is_scalar and (
            self.type_name in SCALAR_TIMELINE_TYPES
            or "weather" in self.categories
            or self.name in {"SessionNum", "SessionState", "CarLeftRight", "PlayerTrackSurface"}
        )

    def sample(self, payload: bytes, frame_context: dict[str, Any]) -> None:
        values = [read_scalar(payload, self.variable, index) for index in range(self.count)]
        cleaned_values = [compact_value(value) for value in values]
        comparable = tuple(compare_value(value) for value in values) if self.count > 1 else compare_value(values[0])

        self.frame_count += 1
        self.value_count += len(values)
        if not self.has_first_value:
            self.has_first_value = True
            self.first_value = cleaned_values[0] if self.is_scalar else None
            if self.timeline_enabled:
                self.add_timeline_event(frame_context, cleaned_values[0])

        if self.previous_value is not None and comparable != self.previous_value:
            self.change_count += 1
            if self.timeline_enabled:
                self.add_timeline_event(frame_context, cleaned_values[0])
        self.previous_value = comparable
        self.last_value = cleaned_values[0] if self.is_scalar else None

        frame_non_default = False
        for index, value in enumerate(values):
            self.stats.add(value)
            if is_non_default(value):
                frame_non_default = True
                if self.count > 1:
                    index_stats = self.index_stats.setdefault(index, IndexStats())
                    index_stats.add(value)

        if frame_non_default:
            self.non_default_frame_count += 1

        if self.is_scalar and not self.distinct_overflow:
            key = json.dumps(cleaned_values[0], sort_keys=True)
            self.distinct[key] += 1
            if len(self.distinct) > self.max_distinct:
                self.distinct.clear()
                self.distinct_overflow = True

    def add_timeline_event(self, frame_context: dict[str, Any], value: Any) -> None:
        if len(self.timeline) >= self.max_timeline_events:
            self.timeline_truncated = True
            return
        self.timeline.append(
            {
                "capturedMs": frame_context["capturedMs"],
                "frameIndex": frame_context["frameIndex"],
                "sessionTime": clean_float(frame_context["sessionTime"], 3),
                "value": value,
            }
        )

    def distinct_values_json(self) -> list[dict[str, Any]] | None:
        if self.distinct_overflow or not self.distinct:
            return None
        values: list[dict[str, Any]] = []
        for encoded, count in self.distinct.most_common():
            values.append({"value": json.loads(encoded), "frameCount": count})
        return values

    def top_indexes_json(self, limit: int) -> list[dict[str, Any]]:
        return [
            stats.to_json(index)
            for index, stats in sorted(
                self.index_stats.items(),
                key=lambda item: (item[1].non_default_frames, item[1].change_count),
                reverse=True,
            )[:limit]
        ]

    def to_json(self, top_index_limit: int) -> dict[str, Any]:
        field = {
            "name": self.name,
            "typeName": self.type_name,
            "count": self.count,
            "unit": self.unit,
            "description": self.description,
            "categories": self.categories,
            "sampledFrameCount": self.frame_count,
            "sampledValueCount": self.value_count,
            "nonDefaultFrameCount": self.non_default_frame_count,
            "changeCount": self.change_count,
            "firstValue": self.first_value,
            "lastValue": self.last_value,
            "distinctValues": self.distinct_values_json(),
            "distinctValueCount": None if self.distinct_overflow else len(self.distinct),
            "distinctValuesTruncated": self.distinct_overflow,
            **self.stats.to_json(),
        }
        if self.count > 1:
            field["activeIndexCount"] = len(self.index_stats)
            field["topActiveIndexes"] = self.top_indexes_json(top_index_limit)
        if self.timeline:
            field["timeline"] = self.timeline
            field["timelineTruncated"] = self.timeline_truncated
        return field


def read_capture_header(stream: Any) -> dict[str, Any]:
    header = stream.read(FILE_HEADER.size)
    if len(header) != FILE_HEADER.size:
        raise RuntimeError("Invalid telemetry.bin file header")
    magic, sdk_version, tick_rate, buffer_length, variable_count, capture_start_ms = FILE_HEADER.unpack(header)
    return {
        "magic": magic.decode("ascii", errors="replace").rstrip("\x00"),
        "sdkVersion": sdk_version,
        "tickRate": tick_rate,
        "bufferLength": buffer_length,
        "variableCount": variable_count,
        "captureStartUnixMs": capture_start_ms,
    }


def frame_context(captured_ms: int, frame_index: int, session_tick: int, session_info_update: int, session_time: float) -> dict[str, Any]:
    return {
        "capturedMs": captured_ms,
        "frameIndex": frame_index,
        "sessionTick": session_tick,
        "sessionInfoUpdate": session_info_update,
        "sessionTime": session_time,
    }


def synthesize_capture(
    capture_dir: Path,
    sample_stride: int,
    auto_sample_target: int,
    max_frames: int | None,
    max_distinct: int,
    max_timeline_events: int,
    top_index_limit: int,
) -> dict[str, Any]:
    schema_rows = load_json(capture_dir / "telemetry-schema.json")
    variables = [dict(row) for row in schema_rows]
    field_stats = [
        FieldStats(variable, max_distinct=max_distinct, max_timeline_events=max_timeline_events)
        for variable in variables
    ]
    manifest = load_json(capture_dir / "capture-manifest.json") if (capture_dir / "capture-manifest.json").exists() else {}
    latest_session = load_yaml(capture_dir / "latest-session.yaml")
    telemetry_path = capture_dir / "telemetry.bin"
    file_size = telemetry_path.stat().st_size
    frame_count_hint = int(manifest.get("frameCount") or 0)
    resolved_sample_stride = sample_stride
    if resolved_sample_stride == 0:
        target = max(1, auto_sample_target)
        resolved_sample_stride = max(1, math.ceil(frame_count_hint / target)) if frame_count_hint > target else 1

    total_frames = 0
    sampled_frames = 0
    first_frame_context: dict[str, Any] | None = None
    last_frame_context: dict[str, Any] | None = None
    session_nums = Counter()
    session_info_updates = Counter()

    with telemetry_path.open("rb") as stream:
        capture_header = read_capture_header(stream)
        while True:
            raw_header = stream.read(FRAME_HEADER.size)
            if not raw_header:
                break
            if len(raw_header) != FRAME_HEADER.size:
                raise RuntimeError("Truncated telemetry frame header")
            captured_ms, frame_index, session_tick, session_info_update, session_time, payload_length = FRAME_HEADER.unpack(raw_header)
            payload = stream.read(payload_length)
            if len(payload) != payload_length:
                raise RuntimeError("Truncated telemetry payload")

            total_frames += 1
            context = frame_context(captured_ms, frame_index, session_tick, session_info_update, session_time)
            first_frame_context = first_frame_context or context
            last_frame_context = context
            session_info_updates[session_info_update] += 1

            if resolved_sample_stride > 1 and (total_frames - 1) % resolved_sample_stride != 0:
                continue
            if max_frames is not None and sampled_frames >= max_frames:
                continue

            sampled_frames += 1
            session_num_variable = next((variable for variable in variables if variable["name"] == "SessionNum"), None)
            if session_num_variable is not None:
                session_nums[read_scalar(payload, session_num_variable)] += 1

            for stats in field_stats:
                stats.sample(payload, context)

    fields = [stats.to_json(top_index_limit) for stats in field_stats]
    fields_by_name = {field["name"]: field for field in fields}
    changed_fields = sorted(
        (field for field in fields if field["changeCount"] > 0),
        key=lambda field: field["changeCount"],
        reverse=True,
    )
    active_array_fields = sorted(
        (field for field in fields if field.get("activeIndexCount")),
        key=lambda field: field.get("activeIndexCount") or 0,
        reverse=True,
    )
    weather_fields = [
        field
        for field in fields
        if "weather" in field["categories"]
    ]
    radar_like_fields = [
        field
        for field in fields
        if contains_any(f"{field['name']} {field.get('unit')} {field.get('description')}", RADAR_TERMS)
    ]
    category_counts = Counter(
        category
        for field in fields
        for category in field["categories"]
    )

    return {
        "synthesisVersion": 1,
        "generatedAtUtc": utc_now(),
        "captureId": capture_dir.name,
        "captureDirectory": str(capture_dir),
        "sourceFiles": {
            "hasManifest": bool(manifest),
            "hasLatestSessionYaml": bool(latest_session),
            "telemetryBytes": file_size,
        },
        "captureManifest": {
            "startedAtUtc": manifest.get("startedAtUtc"),
            "finishedAtUtc": manifest.get("finishedAtUtc"),
            "frameCount": manifest.get("frameCount"),
            "droppedFrameCount": manifest.get("droppedFrameCount"),
            "sessionInfoSnapshotCount": manifest.get("sessionInfoSnapshotCount"),
        },
        "captureHeader": capture_header,
        "frameScan": {
            "totalFrameRecords": total_frames,
            "sampleStride": resolved_sample_stride,
            "sampledFrameCount": sampled_frames,
            "autoSampleTarget": auto_sample_target if sample_stride == 0 else None,
            "maxFrames": max_frames,
            "firstFrame": first_frame_context,
            "lastFrame": last_frame_context,
            "sessionNumsInSample": [
                {"sessionNum": key, "frameCount": value}
                for key, value in session_nums.most_common()
            ],
            "sessionInfoUpdateCount": len(session_info_updates),
        },
        "sessionInfo": {
            "weekendInfo": latest_session.get("WeekendInfo") or {},
            "currentSessionNum": (latest_session.get("SessionInfo") or {}).get("CurrentSessionNum"),
        },
        "schemaSummary": {
            "fieldCount": len(fields),
            "arrayFieldCount": sum(1 for field in fields if field["count"] > 1),
            "typeCounts": Counter(field["typeName"] for field in fields),
            "categoryCounts": dict(sorted(category_counts.items())),
        },
        "interestingFields": {
            "mostChanged": changed_fields[:50],
            "activeArrays": active_array_fields[:50],
            "constantNonDefault": [
                field
                for field in fields
                if field["changeCount"] == 0 and field["nonDefaultFrameCount"] > 0
            ][:100],
        },
        "weather": {
            "fieldNames": [field["name"] for field in weather_fields],
            "fields": weather_fields,
            "radarLikeFieldNames": [field["name"] for field in radar_like_fields],
            "radarLikeFields": radar_like_fields,
            "hasExplicitRadarTelemetryField": any(
                contains_any(f"{field['name']} {field.get('description')}", ["radar"])
                for field in radar_like_fields
            ),
            "notes": [
                "This section is derived from the same all-field synthesis as every other variable.",
                "It summarizes scalar weather and radar-like telemetry fields. It does not capture pixels from the iRacing on-screen weather radar.",
            ],
        },
        "fields": fields,
        "fieldsByName": fields_by_name,
    }


def size_label(size_bytes: int) -> str:
    return f"{size_bytes / 1024 / 1024:.2f} MiB"


def main() -> None:
    parser = argparse.ArgumentParser(description="Create a compact all-telemetry synthesis from a raw TmrOverlay capture.")
    parser.add_argument("--capture", type=Path, required=True, help="Path to a capture-* directory.")
    parser.add_argument("--output", type=Path, help="Output JSON path. Defaults to <capture>/capture-synthesis.json.")
    parser.add_argument("--sample-stride", type=int, default=0, help="Sample every Nth frame. Defaults to 0, which auto-strides large captures.")
    parser.add_argument("--auto-sample-target", type=int, default=DEFAULT_AUTO_SAMPLE_TARGET, help="Target sampled frame count when --sample-stride is 0.")
    parser.add_argument("--max-frames", type=int, default=None, help="Optional maximum sampled frame count.")
    parser.add_argument("--max-distinct", type=int, default=32, help="Maximum scalar distinct values to keep per field.")
    parser.add_argument("--max-timeline-events", type=int, default=200, help="Maximum scalar timeline events per eligible field.")
    parser.add_argument("--top-indexes", type=int, default=12, help="Maximum active indexes to keep for array fields.")
    parser.add_argument("--max-output-mib", type=float, default=24.0, help="Fail if the JSON output exceeds this size. Default stays below GitHub's 25 MiB browser upload cap.")
    parser.add_argument("--allow-while-iracing-running", action="store_true", help="Override the Windows safety guard that refuses to synthesize while the iRacing sim process is running.")
    args = parser.parse_args()

    if args.sample_stride < 0:
        raise SystemExit("--sample-stride must be >= 0")
    if args.auto_sample_target < 1:
        raise SystemExit("--auto-sample-target must be >= 1")
    running_processes = running_iracing_sim_processes()
    if running_processes and not args.allow_while_iracing_running:
        raise SystemExit(
            "Refusing to synthesize while the iRacing sim process is running: "
            f"{', '.join(running_processes)}. Close the sim first, or pass "
            "--allow-while-iracing-running only for an intentional offline/debug run."
        )

    output_path = args.output or (args.capture / "capture-synthesis.json")
    synthesis = synthesize_capture(
        capture_dir=args.capture,
        sample_stride=args.sample_stride,
        auto_sample_target=args.auto_sample_target,
        max_frames=args.max_frames,
        max_distinct=args.max_distinct,
        max_timeline_events=args.max_timeline_events,
        top_index_limit=args.top_indexes,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(synthesis, indent=2, default=str) + "\n", encoding="utf-8")
    output_size = output_path.stat().st_size
    output_limit = int(args.max_output_mib * 1024 * 1024)
    print(f"Wrote {output_path}")
    print(
        "Telemetry synthesis: "
        f"{synthesis['frameScan']['sampledFrameCount']} sampled frames from "
        f"{synthesis['frameScan']['totalFrameRecords']} records, "
        f"{synthesis['schemaSummary']['fieldCount']} fields, "
        f"sample stride {synthesis['frameScan']['sampleStride']}"
    )
    print(f"Output size: {size_label(output_size)} / {args.max_output_mib:.2f} MiB budget")
    if synthesis["weather"]["hasExplicitRadarTelemetryField"]:
        print("Weather radar-like telemetry fields were found.")
    else:
        print("No explicit telemetry field named/described as radar was found.")
    if output_size > output_limit:
        raise SystemExit(
            "Capture synthesis exceeded the upload budget. "
            "Rerun with a larger --sample-stride, lower --max-timeline-events, or lower --top-indexes."
        )


if __name__ == "__main__":
    main()
