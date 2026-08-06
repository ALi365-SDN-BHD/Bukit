"""Delta-only final-review scope generation."""

from __future__ import annotations

import argparse
from pathlib import Path
import sys
from typing import Any, Dict, List

from .common import SCHEMA_VERSION, WorkflowError, _json_bytes, _load_json

def _validate_review_evidence(path: Path) -> Dict[str, Any]:
    evidence = _load_json(path)
    if not isinstance(evidence, dict):
        raise WorkflowError(f"review evidence root must be an object: {path}")
    required = {
        "cacheStatus",
        "closureFiles",
        "publicContractFiles",
        "schemaVersion",
        "taskId",
    }
    if set(evidence) != required:
        raise WorkflowError(f"review evidence has missing or unknown fields: {path}")
    if evidence["schemaVersion"] != SCHEMA_VERSION:
        raise WorkflowError(f"unsupported review evidence schemaVersion: {path}")
    if evidence["cacheStatus"] not in {"hit", "miss"}:
        raise WorkflowError(f"invalid review evidence cacheStatus: {path}")
    if not isinstance(evidence["taskId"], str) or not evidence["taskId"]:
        raise WorkflowError(f"review evidence taskId must be non-empty: {path}")
    for field in ("closureFiles", "publicContractFiles"):
        if not isinstance(evidence[field], list) or not all(
            isinstance(item, str) and item for item in evidence[field]
        ):
            raise WorkflowError(f"review evidence {field} must be a string array: {path}")
    return evidence


def _validate_findings(path: Path) -> List[Dict[str, Any]]:
    document = _load_json(path)
    if not isinstance(document, dict) or set(document) != {
        "findings",
        "schemaVersion",
    }:
        raise WorkflowError("findings document has missing or unknown fields")
    if document["schemaVersion"] != SCHEMA_VERSION:
        raise WorkflowError("unsupported findings schemaVersion")
    if not isinstance(document["findings"], list):
        raise WorkflowError("findings must be an array")
    required = {"files", "id", "severity", "status"}
    findings = []
    for index, finding in enumerate(document["findings"]):
        if not isinstance(finding, dict) or set(finding) != required:
            raise WorkflowError(f"finding {index} has missing or unknown fields")
        if finding["severity"] not in {"Critical", "Important", "Minor"}:
            raise WorkflowError(f"finding {index} has invalid severity")
        if finding["status"] not in {"open", "resolved"}:
            raise WorkflowError(f"finding {index} has invalid status")
        if not isinstance(finding["id"], str) or not finding["id"]:
            raise WorkflowError(f"finding {index} has invalid id")
        if not isinstance(finding["files"], list) or not all(
            isinstance(item, str) and item for item in finding["files"]
        ):
            raise WorkflowError(f"finding {index} files must be a string array")
        findings.append(finding)
    return findings


def _review_scope(arguments: argparse.Namespace) -> int:
    evidence = [
        _validate_review_evidence(Path(path).resolve()) for path in arguments.evidence
    ]
    task_ids = [item["taskId"] for item in evidence]
    if len(set(task_ids)) != len(task_ids):
        raise WorkflowError("review evidence taskId values must be unique")

    reusable = sorted(
        item["taskId"] for item in evidence if item["cacheStatus"] == "hit"
    )
    invalidated = sorted(
        item["taskId"] for item in evidence if item["cacheStatus"] == "miss"
    )
    tasks_by_file: Dict[str, set[str]] = {}
    for item in evidence:
        for relative in set(item["closureFiles"]):
            tasks_by_file.setdefault(relative, set()).add(item["taskId"])
    intersections = [
        {"file": relative, "tasks": sorted(tasks)}
        for relative, tasks in sorted(tasks_by_file.items())
        if len(tasks) > 1
    ]

    reusable_coverage = {
        relative
        for item in evidence
        if item["cacheStatus"] == "hit"
        for relative in item["closureFiles"]
    }
    changed = sorted(set(arguments.changed))
    uncovered = sorted(set(changed) - reusable_coverage)
    public_contract = sorted(
        {
            relative
            for item in evidence
            for relative in item["publicContractFiles"]
            if relative in changed
        }
    )
    blocking_findings = sorted(
        (
            finding
            for finding in _validate_findings(Path(arguments.findings).resolve())
            if finding["status"] == "open"
            and finding["severity"] in {"Critical", "Important"}
        ),
        key=lambda finding: finding["id"],
    )

    review_files = set(uncovered) | set(public_contract)
    review_files.update(item["file"] for item in intersections)
    review_files.update(
        relative
        for item in evidence
        if item["cacheStatus"] == "miss"
        for relative in item["closureFiles"]
    )
    review_files.update(
        relative for finding in blocking_findings for relative in finding["files"]
    )
    result = {
        "crossTaskIntersections": intersections,
        "invalidatedEvidence": invalidated,
        "openBlockingFindings": blocking_findings,
        "publicContractFocus": public_contract,
        "reusableEvidence": reusable,
        "reviewFiles": sorted(review_files),
        "schemaVersion": SCHEMA_VERSION,
        "uncoveredChangedFiles": uncovered,
    }
    sys.stdout.buffer.write(_json_bytes(result))
    return 0

