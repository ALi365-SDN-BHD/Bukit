"""Workflow speed-metrics persistence and reporting."""

from __future__ import annotations

import argparse
from collections import Counter
from pathlib import Path
import re
import sys
from typing import Any, Dict

from .common import (
    SCHEMA_VERSION,
    WorkflowError,
    _atomic_json_write,
    _json_bytes,
    _load_json,
    _mutable_state_path,
)
from .queue import _exclusive_file_lock

def _validate_metrics_state(document: Any, path: Path) -> Dict[str, Any]:
    if not isinstance(document, dict) or set(document) != {
        "events",
        "schemaVersion",
    }:
        raise WorkflowError(f"metrics state has missing or unknown fields: {path}")
    if document["schemaVersion"] != SCHEMA_VERSION:
        raise WorkflowError(f"unsupported metrics schemaVersion: {path}")
    if not isinstance(document["events"], list):
        raise WorkflowError(f"metrics events must be an array: {path}")
    required = {
        "cacheStatus",
        "commandLabel",
        "conflict",
        "durationMs",
        "phase",
        "rerun",
        "status",
        "taskId",
    }
    for index, event in enumerate(document["events"]):
        if not isinstance(event, dict) or set(event) != required:
            raise WorkflowError(f"metrics event {index} has invalid fields: {path}")
        if (
            not isinstance(event["durationMs"], int)
            or isinstance(event["durationMs"], bool)
            or event["durationMs"] < 0
        ):
            raise WorkflowError(f"metrics event {index} has invalid durationMs: {path}")
        if event["phase"] not in {"implementation", "test", "review", "idle"}:
            raise WorkflowError(f"metrics event {index} has invalid phase: {path}")
        if event["cacheStatus"] not in {"hit", "miss", "none"}:
            raise WorkflowError(
                f"metrics event {index} has invalid cacheStatus: {path}"
            )
        label = event["commandLabel"]
        if label is not None and (
            not isinstance(label, str) or not _COMMAND_LABEL.fullmatch(label)
        ):
            raise WorkflowError(
                f"metrics event {index} has invalid commandLabel: {path}"
            )
        if type(event["conflict"]) is not bool or type(event["rerun"]) is not bool:
            raise WorkflowError(f"metrics event {index} has invalid flags: {path}")
        if event["status"] not in {"completed", "blocked"}:
            raise WorkflowError(f"metrics event {index} has invalid status: {path}")
        if not isinstance(event["taskId"], str) or not event["taskId"]:
            raise WorkflowError(f"metrics event {index} has invalid taskId: {path}")
    return document


_COMMAND_LABEL = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{0,79}$")


def _metrics_add(arguments: argparse.Namespace) -> int:
    if not arguments.task:
        raise WorkflowError("metrics task must be non-empty")
    if arguments.duration_ms < 0:
        raise WorkflowError("metrics duration must be non-negative")
    if arguments.command_label and not _COMMAND_LABEL.fullmatch(
        arguments.command_label
    ):
        raise WorkflowError(
            "metrics command-label must be a short identifier, not a raw command"
        )
    state_path = _mutable_state_path(arguments.state)
    state_path.parent.mkdir(parents=True, exist_ok=True)
    event = {
        "cacheStatus": arguments.cache_status,
        "commandLabel": arguments.command_label,
        "conflict": arguments.conflict,
        "durationMs": arguments.duration_ms,
        "phase": arguments.phase,
        "rerun": arguments.rerun,
        "status": arguments.status,
        "taskId": arguments.task,
    }
    with _exclusive_file_lock(state_path, "METRICS"):
        if state_path.exists():
            state = _validate_metrics_state(_load_json(state_path), state_path)
        else:
            state = {"events": [], "schemaVersion": SCHEMA_VERSION}
        state["events"].append(event)
        _atomic_json_write(state_path, state)
    print(f"METRICS ADDED {arguments.task} {arguments.phase}")
    return 0


def _metrics_report(arguments: argparse.Namespace) -> int:
    state_path = _mutable_state_path(arguments.state)
    state = _validate_metrics_state(_load_json(state_path), state_path)
    events = state["events"]
    phase_totals: Counter[str] = Counter()
    task_totals: Counter[str] = Counter()
    status_counts: Counter[str] = Counter()
    labels: Counter[str] = Counter()
    hits = 0
    misses = 0
    reruns = 0
    conflicts = 0
    for event in events:
        phase_totals[event["phase"]] += event["durationMs"]
        task_totals[event["taskId"]] += event["durationMs"]
        status_counts[event["status"]] += 1
        if event["commandLabel"]:
            labels[event["commandLabel"]] += 1
        hits += int(event["cacheStatus"] == "hit")
        misses += int(event["cacheStatus"] == "miss")
        reruns += int(event["rerun"])
        conflicts += int(event["conflict"])
    attempts = hits + misses
    result = {
        "cache": {
            "hitRate": round(hits / attempts, 4) if attempts else None,
            "hits": hits,
            "misses": misses,
        },
        "conflictCount": conflicts,
        "duplicateCommandLabels": [
            {"count": count, "label": label}
            for label, count in sorted(labels.items())
            if count > 1
        ],
        "eventCount": len(events),
        "phaseDurationsMs": dict(sorted(phase_totals.items())),
        "rerunCount": reruns,
        "schemaVersion": SCHEMA_VERSION,
        "statusCounts": dict(sorted(status_counts.items())),
        "taskTotalsMs": dict(sorted(task_totals.items())),
    }
    sys.stdout.buffer.write(_json_bytes(result))
    return 0

