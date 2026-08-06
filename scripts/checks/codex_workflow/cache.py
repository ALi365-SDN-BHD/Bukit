"""GREEN evidence cache recording and validation."""

from __future__ import annotations

import argparse
import datetime as dt
import re
from typing import Any, Dict, List

from .common import (
    SCHEMA_VERSION,
    WorkflowError,
    _atomic_json_write,
    _load_json,
    _mutable_state_path,
)
from .repo import _fingerprint, _fingerprint_inputs

def _cache_record(arguments: argparse.Namespace) -> int:
    if arguments.duration_ms < 0:
        raise WorkflowError("cache duration must be non-negative")
    inputs = _fingerprint_inputs(arguments)
    record = {
        "durationMs": arguments.duration_ms,
        "exitCode": arguments.exit_code,
        "fingerprint": _fingerprint(inputs),
        "fingerprintInputs": inputs,
        "recordedAt": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "result": arguments.result,
        "schemaVersion": SCHEMA_VERSION,
    }
    _atomic_json_write(_mutable_state_path(arguments.record), record)
    print(f"CACHE RECORDED {record['fingerprint']}")
    return 0


_HEX_DIGEST = re.compile(r"^[0-9a-f]{64}$")
_GIT_OBJECT_ID = re.compile(r"^(?:[0-9a-f]{40}|[0-9a-f]{64})$")


def _validate_fingerprint_inputs(inputs: Any) -> Dict[str, Any]:
    required = {"baseHead", "closure", "command", "environment", "sdkVersion"}
    if not isinstance(inputs, dict) or set(inputs) != required:
        raise WorkflowError(
            "cache record fingerprintInputs has missing or unknown fields"
        )
    if not isinstance(inputs["baseHead"], str) or not _GIT_OBJECT_ID.fullmatch(
        inputs["baseHead"]
    ):
        raise WorkflowError("cache record fingerprintInputs baseHead is invalid")
    if not isinstance(inputs["command"], str) or not inputs["command"]:
        raise WorkflowError("cache record fingerprintInputs command is invalid")
    if not isinstance(inputs["sdkVersion"], str) or not inputs["sdkVersion"]:
        raise WorkflowError("cache record fingerprintInputs sdkVersion is invalid")
    environment = inputs["environment"]
    if not isinstance(environment, dict) or not all(
        isinstance(name, str)
        and name
        and "=" not in name
        and state in {"set", "empty", "unset"}
        for name, state in environment.items()
    ):
        raise WorkflowError("cache record fingerprintInputs environment is invalid")
    closure = inputs["closure"]
    if not isinstance(closure, list):
        raise WorkflowError("cache record fingerprintInputs closure is invalid")
    paths = set()
    for index, item in enumerate(closure):
        if not isinstance(item, dict) or set(item) != {"kind", "path", "sha256"}:
            raise WorkflowError(
                f"cache record fingerprintInputs closure[{index}] is invalid"
            )
        if item["kind"] not in {"file", "missing", "symlink"}:
            raise WorkflowError(
                f"cache record fingerprintInputs closure[{index}] kind is invalid"
            )
        if not isinstance(item["path"], str) or not item["path"]:
            raise WorkflowError(
                f"cache record fingerprintInputs closure[{index}] path is invalid"
            )
        if item["path"] in paths:
            raise WorkflowError(
                "cache record fingerprintInputs closure contains duplicate paths"
            )
        paths.add(item["path"])
        digest = item["sha256"]
        if item["kind"] == "missing":
            valid_digest = digest is None
        else:
            valid_digest = isinstance(digest, str) and bool(
                _HEX_DIGEST.fullmatch(digest)
            )
        if not valid_digest:
            raise WorkflowError(
                f"cache record fingerprintInputs closure[{index}] sha256 is invalid"
            )
    return inputs


def _validate_cache_record(record: Any) -> Dict[str, Any]:
    if not isinstance(record, dict):
        raise WorkflowError("cache record root must be an object")
    required = {
        "durationMs",
        "exitCode",
        "fingerprint",
        "fingerprintInputs",
        "recordedAt",
        "result",
        "schemaVersion",
    }
    if set(record) != required:
        raise WorkflowError("cache record has missing or unknown fields")
    if record["schemaVersion"] != SCHEMA_VERSION:
        raise WorkflowError("unsupported cache record schemaVersion")
    if record["result"] not in {"passed", "failed"}:
        raise WorkflowError("cache record result must be passed or failed")
    if (
        not isinstance(record["durationMs"], int)
        or isinstance(record["durationMs"], bool)
        or record["durationMs"] < 0
    ):
        raise WorkflowError("cache record durationMs is invalid")
    if not isinstance(record["exitCode"], int) or isinstance(
        record["exitCode"], bool
    ):
        raise WorkflowError("cache record exitCode is invalid")
    if not isinstance(record["recordedAt"], str) or not record["recordedAt"]:
        raise WorkflowError("cache record recordedAt is invalid")
    inputs = _validate_fingerprint_inputs(record["fingerprintInputs"])
    if not isinstance(record["fingerprint"], str) or not _HEX_DIGEST.fullmatch(
        record["fingerprint"]
    ):
        raise WorkflowError("cache record fingerprint is invalid")
    if record["fingerprint"] != _fingerprint(inputs):
        raise WorkflowError("cache record fingerprint is invalid")
    return record


def _cache_check(arguments: argparse.Namespace) -> int:
    record = _validate_cache_record(
        _load_json(_mutable_state_path(arguments.record))
    )
    current = _fingerprint_inputs(arguments)
    recorded = record["fingerprintInputs"]
    reasons: List[str] = []
    comparisons = [
        ("base", "baseHead"),
        ("closure", "closure"),
        ("command", "command"),
        ("environment", "environment"),
        ("sdk", "sdkVersion"),
    ]
    for reason, field in comparisons:
        if recorded.get(field) != current.get(field):
            reasons.append(reason)
    if record["result"] != "passed" or record["exitCode"] != 0:
        reasons.append("result")
    if reasons:
        print(f"CACHE MISS: {', '.join(reasons)}")
        return 1
    print(f"CACHE HIT {record['fingerprint']}")
    return 0

