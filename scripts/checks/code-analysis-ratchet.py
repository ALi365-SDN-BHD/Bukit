#!/usr/bin/env python3
from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import re
import sys
from typing import Any


DIAGNOSTIC_ID = re.compile(r"^[A-Z]+[0-9]+$")


class GateError(Exception):
    pass


def usage() -> None:
    print(
        "usage: python3 scripts/checks/code-analysis-ratchet.py "
        "<compare BASELINE STYLE_REPORT ANALYZER_REPORT|"
        "snapshot OUTPUT STYLE_REPORT ANALYZER_REPORT>",
        file=sys.stderr,
    )


def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise GateError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> Any:
    try:
        payload = path.read_bytes()
    except OSError as error:
        raise GateError(f"cannot read {path}: {error.strerror or error}") from error
    if payload.startswith(b"\xef\xbb\xbf"):
        raise GateError(f"{path} must be UTF-8 without BOM")
    try:
        text = payload.decode("utf-8")
    except UnicodeDecodeError as error:
        raise GateError(f"{path} is not valid UTF-8") from error
    try:
        return json.loads(text, object_pairs_hook=reject_duplicate_keys)
    except GateError:
        raise
    except json.JSONDecodeError as error:
        raise GateError(f"{path} is not valid JSON: line {error.lineno} column {error.colno}") from error


def parse_counts(value: Any, category: str) -> dict[str, int]:
    if not isinstance(value, dict):
        raise GateError(f"baseline {category} must be an object")
    counts: dict[str, int] = {}
    for diagnostic, count in value.items():
        if not isinstance(diagnostic, str) or DIAGNOSTIC_ID.fullmatch(diagnostic) is None:
            raise GateError(f"baseline {category} contains invalid diagnostic ID: {diagnostic!r}")
        if not isinstance(count, int) or isinstance(count, bool) or count < 0:
            raise GateError(f"baseline {category} {diagnostic} count must be a non-negative integer")
        counts[diagnostic] = count
    if list(counts) != sorted(counts):
        raise GateError(f"baseline {category} diagnostic IDs must be sorted")
    return counts


def load_baseline(path: Path) -> dict[str, dict[str, int]]:
    value = load_json(path)
    if not isinstance(value, dict):
        raise GateError("baseline root must be an object")
    expected_keys = ["schemaVersion", "style", "analyzers"]
    if list(value) != expected_keys:
        raise GateError(f"baseline keys must be exactly: {', '.join(expected_keys)}")
    if value["schemaVersion"] != 1:
        raise GateError("baseline schemaVersion must be 1")
    return {
        "style": parse_counts(value["style"], "style"),
        "analyzers": parse_counts(value["analyzers"], "analyzers"),
    }


def report_counts(path: Path) -> dict[str, int]:
    value = load_json(path)
    if not isinstance(value, list):
        raise GateError(f"formatter report {path} root must be an array")
    counts: Counter[str] = Counter()
    for document_index, document in enumerate(value):
        if not isinstance(document, dict):
            raise GateError(f"formatter report {path} document {document_index} must be an object")
        changes = document.get("FileChanges")
        if not isinstance(changes, list):
            raise GateError(f"formatter report {path} document {document_index} lacks FileChanges")
        for change_index, change in enumerate(changes):
            if not isinstance(change, dict):
                raise GateError(
                    f"formatter report {path} change {document_index}:{change_index} must be an object"
                )
            diagnostic = change.get("DiagnosticId")
            if not isinstance(diagnostic, str) or DIAGNOSTIC_ID.fullmatch(diagnostic) is None:
                raise GateError(
                    f"formatter report {path} change {document_index}:{change_index} "
                    "contains an invalid DiagnosticId"
                )
            counts[diagnostic] += 1
    return dict(sorted(counts.items()))


def compare(baseline_path: Path, style_path: Path, analyzer_path: Path) -> int:
    baseline = load_baseline(baseline_path)
    current = {
        "style": report_counts(style_path),
        "analyzers": report_counts(analyzer_path),
    }
    regressions: list[str] = []
    for category in ("style", "analyzers"):
        baseline_total = sum(baseline[category].values())
        current_total = sum(current[category].values())
        print(f"code analysis {category}: {current_total}/{baseline_total}")
        for diagnostic, count in current[category].items():
            allowed = baseline[category].get(diagnostic, 0)
            if count > allowed:
                regressions.append(
                    f"regression: {category} {diagnostic} current {count} exceeds baseline {allowed}"
                )
    if regressions:
        for regression in regressions:
            print(regression, file=sys.stderr)
        return 1
    return 0


def snapshot(output: Path, style_path: Path, analyzer_path: Path) -> int:
    payload = {
        "schemaVersion": 1,
        "style": report_counts(style_path),
        "analyzers": report_counts(analyzer_path),
    }
    try:
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(payload, stream, indent=2, ensure_ascii=True)
            stream.write("\n")
    except FileExistsError as error:
        raise GateError(f"snapshot output already exists: {output}") from error
    except OSError as error:
        raise GateError(f"cannot write {output}: {error.strerror or error}") from error
    print(f"code analysis baseline written: {output}")
    return 0


def main() -> int:
    if len(sys.argv) != 5 or sys.argv[1] not in {"compare", "snapshot"}:
        usage()
        return 2
    mode = sys.argv[1]
    first = Path(sys.argv[2])
    style_path = Path(sys.argv[3])
    analyzer_path = Path(sys.argv[4])
    try:
        if mode == "compare":
            return compare(first, style_path, analyzer_path)
        return snapshot(first, style_path, analyzer_path)
    except GateError as error:
        print(f"gate-error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
