"""Shared state, JSON, and policy primitives for the Codex workflow CLI."""

from __future__ import annotations

import json
import os
from pathlib import Path
import tempfile
from typing import Any, Dict, Iterable



SCHEMA_VERSION = 1


class WorkflowError(Exception):
    """A user-facing workflow input or state error."""


class WorkflowConflict(Exception):
    """A valid operation that cannot proceed because a workflow resource is busy."""


def _lexical_absolute(raw_path: str | os.PathLike[str]) -> Path:
    return Path(os.path.abspath(os.fspath(raw_path)))


def _mutable_state_path(raw_path: str) -> Path:
    path = _lexical_absolute(raw_path)
    if path.is_symlink():
        raise WorkflowError(f"refusing mutable symlink path: {path}")
    return path


def _json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")


def _atomic_json_write(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and not path.is_file():
        raise WorkflowError(f"refusing to replace non-file record: {path}")
    if path.is_symlink():
        raise WorkflowError(f"refusing to replace symlink record: {path}")

    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", dir=str(path.parent)
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(_json_bytes(value))
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary_path, path)
    finally:
        if temporary_path.exists():
            temporary_path.unlink()


def _reject_duplicate_keys(pairs: Iterable[tuple[str, Any]]) -> Dict[str, Any]:
    result: Dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise WorkflowError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def _load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8") as handle:
            return json.load(handle, object_pairs_hook=_reject_duplicate_keys)
    except FileNotFoundError as error:
        raise WorkflowError(f"record does not exist: {path}") from error
    except (OSError, json.JSONDecodeError) as error:
        raise WorkflowError(f"cannot read JSON record {path}: {error}") from error


def _load_policy(raw_path: str) -> Dict[str, Any]:
    policy = _load_json(Path(raw_path).resolve())
    if not isinstance(policy, dict):
        raise WorkflowError("policy root must be an object")
    if policy.get("schemaVersion") != SCHEMA_VERSION:
        raise WorkflowError("unsupported policy schemaVersion")
    if not isinstance(policy.get("pathRules"), list):
        raise WorkflowError("policy pathRules must be an array")
    for index, rule in enumerate(policy["pathRules"]):
        if not isinstance(rule, dict):
            raise WorkflowError(f"policy pathRules[{index}] must be an object")
        required = {
            "contractConsumerGlobs",
            "id",
            "matches",
            "publicContract",
            "resource",
            "specialtyTests",
        }
        if set(rule) != required:
            raise WorkflowError(
                f"policy pathRules[{index}] has missing or unknown fields"
            )
        if not all(
            isinstance(rule[field], list)
            for field in ("contractConsumerGlobs", "matches", "specialtyTests")
        ):
            raise WorkflowError(f"policy pathRules[{index}] list field is invalid")
        if not isinstance(rule["publicContract"], bool):
            raise WorkflowError(
                f"policy pathRules[{index}].publicContract must be boolean"
            )
    if not isinstance(policy.get("resourceRules"), list):
        raise WorkflowError("policy resourceRules must be an array")
    valid_classes = {"static-parallel", "dotnet-serial", "fixture-exclusive"}
    for index, rule in enumerate(policy["resourceRules"]):
        if not isinstance(rule, dict) or set(rule) != {
            "class",
            "commandContains",
            "matches",
        }:
            raise WorkflowError(
                f"policy resourceRules[{index}] has missing or unknown fields"
            )
        if rule["class"] not in valid_classes:
            raise WorkflowError(f"policy resourceRules[{index}].class is invalid")
        if not all(
            isinstance(rule[field], list)
            and all(isinstance(item, str) and item for item in rule[field])
            for field in ("commandContains", "matches")
        ):
            raise WorkflowError(f"policy resourceRules[{index}] list field is invalid")
    return policy

