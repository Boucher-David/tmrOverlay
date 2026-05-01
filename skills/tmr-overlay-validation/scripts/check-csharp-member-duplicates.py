#!/usr/bin/env python3
"""Detect C# compile hazards that are easy to miss without dotnet."""

from __future__ import annotations

import argparse
import bisect
import re
import sys
from dataclasses import dataclass
from pathlib import Path


TYPE_DECL_RE = re.compile(
    r"\b(?P<kind>record(?:\s+(?:class|struct))?|class|struct|interface)\s+"
    r"(?P<name>@?[A-Za-z_][A-Za-z0-9_]*)"
)

IDENT_RE = re.compile(r"@?[A-Za-z_][A-Za-z0-9_]*")
SKIP_DIR_NAMES = {".git", ".vs", "bin", "obj", "artifacts"}
KEYWORDS = {
    "add",
    "class",
    "delegate",
    "enum",
    "event",
    "explicit",
    "get",
    "if",
    "implicit",
    "init",
    "interface",
    "namespace",
    "new",
    "operator",
    "out",
    "params",
    "private",
    "protected",
    "public",
    "readonly",
    "ref",
    "record",
    "remove",
    "required",
    "return",
    "scoped",
    "set",
    "struct",
    "this",
    "where",
}


@dataclass(frozen=True)
class Occurrence:
    name: str
    kind: str
    path: Path
    line: int


@dataclass(frozen=True)
class TypeDeclaration:
    name: str
    kind: str
    path: Path
    line: int
    body_start: int
    body_end: int
    params_start: int | None
    params_end: int | None


@dataclass(frozen=True)
class DuplicateIssue:
    type_decl: TypeDeclaration
    member_name: str
    occurrences: tuple[Occurrence, ...]


@dataclass(frozen=True)
class PrimaryConstructorScopeIssue:
    type_decl: TypeDeclaration
    parameter_name: str
    parameter_line: int
    nested_type_name: str
    nested_type_line: int


def normalize_identifier(identifier: str) -> str:
    return identifier[1:] if identifier.startswith("@") else identifier


def line_starts_for(text: str) -> list[int]:
    starts = [0]
    starts.extend(match.end() for match in re.finditer(r"\n", text))
    return starts


def line_for_offset(line_starts: list[int], offset: int) -> int:
    return bisect.bisect_right(line_starts, offset)


def strip_comments_and_strings(source: str) -> str:
    chars = list(source)
    i = 0
    length = len(source)

    def blank(start: int, end: int) -> None:
        for index in range(start, end):
            if chars[index] != "\n":
                chars[index] = " "

    while i < length:
        if source.startswith("//", i):
            end = source.find("\n", i)
            end = length if end == -1 else end
            blank(i, end)
            i = end
            continue

        if source.startswith("/*", i):
            end = source.find("*/", i + 2)
            end = length if end == -1 else end + 2
            blank(i, end)
            i = end
            continue

        if source.startswith('"""', i):
            quote_count = 3
            while i + quote_count < length and source[i + quote_count] == '"':
                quote_count += 1
            delimiter = '"' * quote_count
            end = source.find(delimiter, i + quote_count)
            end = length if end == -1 else end + quote_count
            blank(i, end)
            i = end
            continue

        if source.startswith('@"', i):
            end = i + 2
            while end < length:
                if source[end] == '"' and end + 1 < length and source[end + 1] == '"':
                    end += 2
                    continue
                if source[end] == '"':
                    end += 1
                    break
                end += 1
            blank(i, end)
            i = end
            continue

        if source[i] == '"':
            end = i + 1
            escaped = False
            while end < length:
                char = source[end]
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == '"':
                    end += 1
                    break
                end += 1
            blank(i, end)
            i = end
            continue

        if source[i] == "'":
            end = i + 1
            escaped = False
            while end < length:
                char = source[end]
                if escaped:
                    escaped = False
                elif char == "\\":
                    escaped = True
                elif char == "'":
                    end += 1
                    break
                end += 1
            blank(i, end)
            i = end
            continue

        i += 1

    return "".join(chars)


def find_matching(text: str, open_index: int, open_char: str, close_char: str) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        char = text[index]
        if char == open_char:
            depth += 1
        elif char == close_char:
            depth -= 1
            if depth == 0:
                return index
    return -1


def skip_whitespace(text: str, index: int) -> int:
    while index < len(text) and text[index].isspace():
        index += 1
    return index


def iter_type_declarations(path: Path, source: str, stripped: str) -> list[TypeDeclaration]:
    line_starts = line_starts_for(source)
    declarations: list[TypeDeclaration] = []

    for match in TYPE_DECL_RE.finditer(stripped):
        kind = match.group("kind").split()[0]
        name = normalize_identifier(match.group("name"))
        cursor = skip_whitespace(stripped, match.end())
        params_start: int | None = None
        params_end: int | None = None

        if kind == "record" and cursor < len(stripped) and stripped[cursor] == "(":
            close_paren = find_matching(stripped, cursor, "(", ")")
            if close_paren != -1:
                params_start = cursor + 1
                params_end = close_paren
                cursor = close_paren + 1

        body_start = -1
        paren_depth = 0
        bracket_depth = 0
        index = cursor
        while index < len(stripped):
            char = stripped[index]
            if char == "(":
                paren_depth += 1
            elif char == ")" and paren_depth > 0:
                paren_depth -= 1
            elif char == "[":
                bracket_depth += 1
            elif char == "]" and bracket_depth > 0:
                bracket_depth -= 1
            elif char == "{" and paren_depth == 0 and bracket_depth == 0:
                body_start = index
                break
            elif char == ";" and paren_depth == 0 and bracket_depth == 0:
                break
            index += 1

        if body_start == -1:
            continue

        body_end = find_matching(stripped, body_start, "{", "}")
        if body_end == -1:
            continue

        declarations.append(
            TypeDeclaration(
                name=name,
                kind=kind,
                path=path,
                line=line_for_offset(line_starts, match.start()),
                body_start=body_start,
                body_end=body_end,
                params_start=params_start,
                params_end=params_end,
            )
        )

    return declarations


def split_top_level_commas(text: str) -> list[tuple[str, int]]:
    parts: list[tuple[str, int]] = []
    start = 0
    paren_depth = 0
    bracket_depth = 0
    brace_depth = 0
    angle_depth = 0

    for index, char in enumerate(text):
        if char == "(":
            paren_depth += 1
        elif char == ")" and paren_depth > 0:
            paren_depth -= 1
        elif char == "[":
            bracket_depth += 1
        elif char == "]" and bracket_depth > 0:
            bracket_depth -= 1
        elif char == "{":
            brace_depth += 1
        elif char == "}" and brace_depth > 0:
            brace_depth -= 1
        elif char == "<":
            angle_depth += 1
        elif char == ">" and angle_depth > 0:
            angle_depth -= 1
        elif (
            char == ","
            and paren_depth == 0
            and bracket_depth == 0
            and brace_depth == 0
            and angle_depth == 0
        ):
            parts.append((text[start:index], start))
            start = index + 1

    parts.append((text[start:], start))
    return parts


def find_top_level_sequence(text: str, sequence: str) -> int:
    paren_depth = 0
    bracket_depth = 0
    brace_depth = 0
    angle_depth = 0
    index = 0
    while index < len(text):
        char = text[index]
        if (
            text.startswith(sequence, index)
            and paren_depth == 0
            and bracket_depth == 0
            and brace_depth == 0
            and angle_depth == 0
        ):
            return index
        if char == "(":
            paren_depth += 1
        elif char == ")" and paren_depth > 0:
            paren_depth -= 1
        elif char == "[":
            bracket_depth += 1
        elif char == "]" and bracket_depth > 0:
            bracket_depth -= 1
        elif char == "{":
            brace_depth += 1
        elif char == "}" and brace_depth > 0:
            brace_depth -= 1
        elif char == "<":
            angle_depth += 1
        elif char == ">" and angle_depth > 0:
            angle_depth -= 1
        index += 1
    return -1


def last_identifier(text: str) -> tuple[str | None, int]:
    matches = list(IDENT_RE.finditer(text))
    if not matches:
        return None, -1
    match = matches[-1]
    return normalize_identifier(match.group(0)), match.start()


def remove_leading_attributes(header: str) -> str:
    previous = None
    result = header.lstrip()
    while previous != result:
        previous = result
        result = re.sub(r"^\[[^\]]*\]\s*", "", result, flags=re.DOTALL).lstrip()
    return result


def extract_record_parameter_occurrences(
    type_decl: TypeDeclaration,
    source: str,
    stripped: str,
) -> list[Occurrence]:
    if type_decl.params_start is None or type_decl.params_end is None:
        return []

    line_starts = line_starts_for(source)
    params_text = stripped[type_decl.params_start : type_decl.params_end]
    occurrences: list[Occurrence] = []

    for segment, segment_start in split_top_level_commas(params_text):
        equals = find_top_level_sequence(segment, "=")
        clean_segment = segment if equals == -1 else segment[:equals]
        name, relative_index = last_identifier(clean_segment)
        if name is None or name in KEYWORDS:
            continue
        absolute_index = type_decl.params_start + segment_start + relative_index
        occurrences.append(
            Occurrence(
                name=name,
                kind="record parameter property",
                path=type_decl.path,
                line=line_for_offset(line_starts, absolute_index),
            )
        )

    return occurrences


def extract_record_parameter_type_references(
    type_decl: TypeDeclaration,
    source: str,
    stripped: str,
) -> list[tuple[str, str, int]]:
    if type_decl.params_start is None or type_decl.params_end is None:
        return []

    line_starts = line_starts_for(source)
    params_text = stripped[type_decl.params_start : type_decl.params_end]
    references: list[tuple[str, str, int]] = []

    for segment, segment_start in split_top_level_commas(params_text):
        equals = find_top_level_sequence(segment, "=")
        clean_segment = segment if equals == -1 else segment[:equals]
        clean_segment = remove_leading_attributes(clean_segment)
        matches = list(IDENT_RE.finditer(clean_segment))
        if len(matches) < 2:
            continue

        parameter_match = matches[-1]
        parameter_name = normalize_identifier(parameter_match.group(0))
        type_text = clean_segment[: parameter_match.start()]
        absolute_parameter_index = type_decl.params_start + segment_start + parameter_match.start()
        parameter_line = line_for_offset(line_starts, absolute_parameter_index)

        for type_match in IDENT_RE.finditer(type_text):
            type_name = normalize_identifier(type_match.group(0))
            if type_name in KEYWORDS:
                continue
            references.append((parameter_name, type_name, parameter_line))

    return references


def extract_member_from_header(
    type_decl: TypeDeclaration,
    source: str,
    header: str,
    header_start: int,
    terminator: str,
) -> list[Occurrence]:
    if not header.strip():
        return []

    stripped_header = remove_leading_attributes(header)
    if not stripped_header or stripped_header.lstrip().startswith(("=", ",", ":")):
        return []

    arrow_index = find_top_level_sequence(stripped_header, "=>")
    has_expression_body = arrow_index != -1
    if has_expression_body:
        stripped_header = stripped_header[:arrow_index]

    equals_index = find_top_level_sequence(stripped_header, "=")
    if equals_index != -1:
        stripped_header = stripped_header[:equals_index]

    if re.search(r"\b(?:operator|implicit|explicit)\b", stripped_header):
        return []

    line_starts = line_starts_for(source)
    type_match = re.search(
        r"\b(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+"
        r"(@?[A-Za-z_][A-Za-z0-9_]*)",
        stripped_header,
    )
    if type_match is not None:
        name = normalize_identifier(type_match.group(1))
        return [
            Occurrence(
                name=name,
                kind="nested type",
                path=type_decl.path,
                line=line_for_offset(line_starts, header_start + header.rfind(type_match.group(1))),
            )
        ]

    paren_index = find_top_level_sequence(stripped_header, "(")
    if paren_index != -1:
        name, relative_index = last_identifier(stripped_header[:paren_index])
        if name is None or name == type_decl.name or name in KEYWORDS:
            return []
        return [
            Occurrence(
                name=name,
                kind="method",
                path=type_decl.path,
                line=line_for_offset(line_starts, header_start + header.rfind(name)),
            )
        ]

    if re.search(r"\bthis\s*\[", stripped_header):
        return []

    occurrences: list[Occurrence] = []
    segments = split_top_level_commas(stripped_header)
    for segment, _ in segments:
        segment_equals = find_top_level_sequence(segment, "=")
        clean_segment = segment if segment_equals == -1 else segment[:segment_equals]
        name, _ = last_identifier(clean_segment)
        if name is None or name in KEYWORDS:
            continue
        if re.search(r"\bevent\b", clean_segment):
            kind = "event"
        elif terminator == "{" or has_expression_body:
            kind = "property"
        else:
            kind = "field"
        occurrences.append(
            Occurrence(
                name=name,
                kind=kind,
                path=type_decl.path,
                line=line_for_offset(line_starts, header_start + header.rfind(name)),
            )
        )

    return occurrences


def extract_body_member_occurrences(
    type_decl: TypeDeclaration,
    source: str,
    stripped: str,
) -> list[Occurrence]:
    occurrences: list[Occurrence] = []
    body = stripped[type_decl.body_start + 1 : type_decl.body_end]
    body_offset = type_decl.body_start + 1
    segment_start = 0
    paren_depth = 0
    bracket_depth = 0
    index = 0

    while index < len(body):
        char = body[index]
        if char == "(":
            paren_depth += 1
        elif char == ")" and paren_depth > 0:
            paren_depth -= 1
        elif char == "[":
            bracket_depth += 1
        elif char == "]" and bracket_depth > 0:
            bracket_depth -= 1
        elif char == "{" and paren_depth == 0 and bracket_depth == 0:
            header = body[segment_start:index]
            occurrences.extend(
                extract_member_from_header(
                    type_decl,
                    source,
                    header,
                    body_offset + segment_start,
                    "{",
                )
            )
            global_open = body_offset + index
            global_close = find_matching(stripped, global_open, "{", "}")
            if global_close == -1:
                break
            index = global_close - body_offset + 1
            segment_start = index
            continue
        elif char == ";" and paren_depth == 0 and bracket_depth == 0:
            header = body[segment_start:index]
            occurrences.extend(
                extract_member_from_header(
                    type_decl,
                    source,
                    header,
                    body_offset + segment_start,
                    ";",
                )
            )
            segment_start = index + 1
        index += 1

    return occurrences


def is_invalid_duplicate(occurrences: list[Occurrence]) -> bool:
    if len(occurrences) <= 1:
        return False
    kinds = {occurrence.kind for occurrence in occurrences}
    return kinds != {"method"}


def analyze_text(path: Path, source: str) -> tuple[list[DuplicateIssue], list[PrimaryConstructorScopeIssue]]:
    stripped = strip_comments_and_strings(source)
    duplicate_issues: list[DuplicateIssue] = []
    scope_issues: list[PrimaryConstructorScopeIssue] = []

    for type_decl in iter_type_declarations(path, source, stripped):
        occurrences = extract_record_parameter_occurrences(type_decl, source, stripped)
        occurrences.extend(extract_body_member_occurrences(type_decl, source, stripped))

        by_name: dict[str, list[Occurrence]] = {}
        for occurrence in occurrences:
            by_name.setdefault(occurrence.name, []).append(occurrence)

        for member_name, duplicate_occurrences in sorted(by_name.items()):
            if is_invalid_duplicate(duplicate_occurrences):
                duplicate_issues.append(
                    DuplicateIssue(
                        type_decl=type_decl,
                        member_name=member_name,
                        occurrences=tuple(duplicate_occurrences),
                    )
                )

        nested_type_lines = {
            occurrence.name: occurrence.line
            for occurrence in occurrences
            if occurrence.kind == "nested type"
        }
        if not nested_type_lines:
            continue

        for parameter_name, type_name, parameter_line in extract_record_parameter_type_references(type_decl, source, stripped):
            if type_name not in nested_type_lines:
                continue
            scope_issues.append(
                PrimaryConstructorScopeIssue(
                    type_decl=type_decl,
                    parameter_name=parameter_name,
                    parameter_line=parameter_line,
                    nested_type_name=type_name,
                    nested_type_line=nested_type_lines[type_name],
                )
            )

    return duplicate_issues, scope_issues


def analyze_file(path: Path) -> tuple[list[DuplicateIssue], list[PrimaryConstructorScopeIssue]]:
    try:
        source = path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        source = path.read_text()
    return analyze_text(path, source)


def collect_csharp_files(paths: list[Path]) -> list[Path]:
    files: list[Path] = []
    for path in paths:
        if path.is_file() and path.suffix == ".cs":
            files.append(path)
            continue
        if not path.is_dir():
            continue
        for candidate in path.rglob("*.cs"):
            if any(part in SKIP_DIR_NAMES for part in candidate.parts):
                continue
            files.append(candidate)
    return sorted(files)


def default_roots() -> list[Path]:
    roots = [Path("src"), Path("tests")]
    return [root for root in roots if root.exists()] or [Path(".")]


def run_self_test() -> int:
    duplicate_sample = """
internal sealed record LiveWeatherSnapshot(string? SkiesLabel)
{
    private static string? SkiesLabel(int? skies) => null;
    private static string? Overloaded(int value) => null;
    private static string? Overloaded(string value) => null;
}
"""
    clean_sample = """
internal sealed record LiveWeatherSnapshot(string? SkiesLabel)
{
    private static string? FormatSkiesLabel(int? skies) => null;
    private static string? Overloaded(int value) => null;
    private static string? Overloaded(string value) => null;
}
"""
    nested_type_scope_sample = """
internal sealed record CaptureHealth(ActivityBadge Activity)
{
    private sealed record ActivityBadge(string Text);
}
"""
    duplicate_issues, duplicate_scope_issues = analyze_text(Path("<self-test-duplicate>"), duplicate_sample)
    clean_issues, clean_scope_issues = analyze_text(Path("<self-test-clean>"), clean_sample)
    nested_duplicate_issues, nested_scope_issues = analyze_text(
        Path("<self-test-nested-type-scope>"),
        nested_type_scope_sample,
    )
    if len(duplicate_issues) != 1 or duplicate_issues[0].member_name != "SkiesLabel":
        print("self-test failed: duplicate record property/helper method was not detected", file=sys.stderr)
        return 1
    if duplicate_scope_issues:
        print("self-test failed: duplicate member sample reported a scope issue", file=sys.stderr)
        return 1
    if clean_issues or clean_scope_issues:
        print("self-test failed: clean sample was reported as an issue", file=sys.stderr)
        return 1
    if nested_duplicate_issues:
        print("self-test failed: nested type scope sample reported a duplicate member", file=sys.stderr)
        return 1
    if len(nested_scope_issues) != 1 or nested_scope_issues[0].nested_type_name != "ActivityBadge":
        print("self-test failed: primary-constructor nested type reference was not detected", file=sys.stderr)
        return 1
    print("self-test passed")
    return 0


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Scan C# files for local compile hazards inside type declarations."
    )
    parser.add_argument(
        "paths",
        nargs="*",
        type=Path,
        help="Files or directories to scan. Defaults to src/ and tests/.",
    )
    parser.add_argument(
        "--self-test",
        action="store_true",
        help="Run a small in-memory validation of the duplicate detector.",
    )
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return run_self_test()

    paths = args.paths or default_roots()
    files = collect_csharp_files(paths)
    if not files:
        print("No C# files found.")
        return 0

    all_duplicate_issues: list[DuplicateIssue] = []
    all_scope_issues: list[PrimaryConstructorScopeIssue] = []
    for path in files:
        duplicate_issues, scope_issues = analyze_file(path)
        all_duplicate_issues.extend(duplicate_issues)
        all_scope_issues.extend(scope_issues)

    if not all_duplicate_issues and not all_scope_issues:
        print(f"No duplicate C# member names or primary-constructor nested-type scope hazards found in {len(files)} files.")
        return 0

    if all_duplicate_issues:
        print("Duplicate C# member names found:")
        for issue in all_duplicate_issues:
            type_decl = issue.type_decl
            print(f"{type_decl.path}:{type_decl.line}: type '{type_decl.name}' duplicates '{issue.member_name}'")
            for occurrence in issue.occurrences:
                print(f"  {occurrence.path}:{occurrence.line}: {occurrence.kind}")

    if all_scope_issues:
        print("Primary-constructor nested-type scope hazards found:")
        for issue in all_scope_issues:
            type_decl = issue.type_decl
            print(
                f"{type_decl.path}:{type_decl.line}: type '{type_decl.name}' primary constructor "
                f"parameter '{issue.parameter_name}' uses nested type '{issue.nested_type_name}'"
            )
            print(f"  {type_decl.path}:{issue.parameter_line}: parameter")
            print(f"  {type_decl.path}:{issue.nested_type_line}: nested type declaration")

    return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
