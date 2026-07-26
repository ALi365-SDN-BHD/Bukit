#!/usr/bin/env python3
"""Deterministic helpers for Bukit's high-speed Codex workflow."""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import datetime as dt
import fcntl
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile
from typing import Any, Dict, Iterable, List


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


def _run_git(repo: Path, *arguments: str) -> str:
    try:
        completed = subprocess.run(
            ["git", "-C", str(repo), *arguments],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        detail = getattr(error, "stderr", "") or str(error)
        raise WorkflowError(f"git command failed: {detail.strip()}") from error
    return completed.stdout.strip()


def _resolved_repo(raw_repo: str) -> Path:
    repo = Path(raw_repo).resolve()
    if not repo.is_dir():
        raise WorkflowError(f"repository does not exist: {repo}")
    _run_git(repo, "rev-parse", "--git-dir")
    return repo


def _relative_repo_path(repo: Path, raw_path: str) -> tuple[str, Path]:
    candidate = Path(raw_path)
    resolved = _lexical_absolute(candidate if candidate.is_absolute() else repo / candidate)
    try:
        relative = resolved.relative_to(repo)
    except ValueError as error:
        raise WorkflowError(f"path is outside repository: {raw_path}") from error
    if not relative.parts:
        raise WorkflowError("repository root cannot be a closure file")
    return relative.as_posix(), resolved


def _file_state(repo: Path, raw_path: str) -> Dict[str, Any]:
    relative, resolved = _relative_repo_path(repo, raw_path)
    if resolved.is_symlink():
        target = os.readlink(resolved)
        digest = hashlib.sha256(target.encode("utf-8")).hexdigest()
        return {"kind": "symlink", "path": relative, "sha256": digest}
    if not resolved.exists():
        return {"kind": "missing", "path": relative, "sha256": None}
    if not resolved.is_file():
        raise WorkflowError(f"closure path must be a file: {relative}")
    digest = hashlib.sha256(resolved.read_bytes()).hexdigest()
    return {"kind": "file", "path": relative, "sha256": digest}


def _environment_state(names: Iterable[str]) -> Dict[str, str]:
    states: Dict[str, str] = {}
    for name in sorted(set(names)):
        if not name or "=" in name:
            raise WorkflowError(f"invalid environment-variable name: {name!r}")
        if name not in os.environ:
            states[name] = "unset"
        elif os.environ[name] == "":
            states[name] = "empty"
        else:
            states[name] = "set"
    return states


def _sdk_version(explicit_version: str | None) -> str:
    if explicit_version:
        return explicit_version
    try:
        completed = subprocess.run(
            ["dotnet", "--version"],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        raise WorkflowError(
            "cannot determine SDK version; pass --sdk-version explicitly"
        ) from error
    version = completed.stdout.strip()
    if not version:
        raise WorkflowError("dotnet --version returned an empty value")
    return version


def _fingerprint_inputs(arguments: argparse.Namespace) -> Dict[str, Any]:
    repo = _resolved_repo(arguments.repo)
    base_head = _run_git(repo, "rev-parse", "--verify", f"{arguments.base}^{{commit}}")
    paths = sorted(
        {_relative_repo_path(repo, raw_path)[0] for raw_path in arguments.path}
    )
    return {
        "baseHead": base_head,
        "closure": [_file_state(repo, path) for path in paths],
        "command": arguments.command,
        "environment": _environment_state(arguments.env),
        "sdkVersion": _sdk_version(arguments.sdk_version),
    }


def _fingerprint(inputs: Dict[str, Any]) -> str:
    canonical = json.dumps(
        inputs, ensure_ascii=False, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _matches(path: str, patterns: Iterable[str]) -> bool:
    return any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)


_CSHARP_TYPE_DECLARATION = re.compile(
    r"\b(?:public|internal|protected|private|file)?\s*"
    r"(?:abstract\s+|sealed\s+|static\s+|partial\s+|readonly\s+|ref\s+)*"
    r"(?:class|record|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)\b"
)


def _read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as error:
        raise WorkflowError(f"cannot read source file {path}: {error}") from error


def _declared_csharp_types(repo: Path, changed_files: Iterable[str]) -> List[str]:
    names = set()
    for relative in changed_files:
        if not relative.endswith(".cs"):
            continue
        source = repo / relative
        if not source.is_file():
            continue
        names.update(_CSHARP_TYPE_DECLARATION.findall(_read_text(source)))
    return sorted(names)


def _direct_source_consumers(
    repo: Path, changed_files: Iterable[str], declared_types: Iterable[str]
) -> List[str]:
    changed = set(changed_files)
    names = list(declared_types)
    if not names:
        return []
    token_pattern = re.compile(
        r"\b(?:" + "|".join(re.escape(name) for name in names) + r")\b"
    )
    consumers = []
    source_root = repo / "src"
    if not source_root.is_dir():
        return consumers
    for candidate in sorted(source_root.rglob("*.cs")):
        relative = candidate.relative_to(repo).as_posix()
        if {"bin", "obj"} & set(candidate.relative_to(repo).parts):
            continue
        if relative in changed:
            continue
        if token_pattern.search(_read_text(candidate)):
            consumers.append(relative)
    return consumers


def _files_matching(repo: Path, patterns: Iterable[str]) -> List[str]:
    matched = []
    pattern_list = list(patterns)
    if not pattern_list:
        return matched
    for candidate in sorted(repo.rglob("*")):
        relative_path = candidate.relative_to(repo)
        if (
            not candidate.is_file()
            or {".git", "bin", "obj"} & set(relative_path.parts)
        ):
            continue
        relative = relative_path.as_posix()
        if _matches(relative, pattern_list):
            matched.append(relative)
    return matched


def _resource_class_for_path(policy: Dict[str, Any], relative: str) -> str:
    for rule in policy["resourceRules"]:
        if _matches(relative, rule["matches"]):
            return rule["class"]
    raise WorkflowError(f"no resource rule matches path: {relative}")


def _resource_class_for_command(policy: Dict[str, Any], command: str) -> str:
    for rule in policy["resourceRules"]:
        if any(token in command for token in rule["commandContains"]):
            return rule["class"]
    return "static-parallel"


def _closure(arguments: argparse.Namespace) -> int:
    repo = _resolved_repo(arguments.repo)
    policy = _load_policy(arguments.policy)
    changed_files = sorted(
        {_relative_repo_path(repo, path)[0] for path in arguments.changed}
    )
    direct_consumers = _direct_source_consumers(
        repo, changed_files, _declared_csharp_types(repo, changed_files)
    )

    specialty_tests = set()
    contract_patterns = set()
    public_contract_files = []
    unmapped_files = []
    for changed in changed_files:
        matching_rules = [
            rule
            for rule in policy["pathRules"]
            if _matches(changed, rule["matches"])
        ]
        if not matching_rules:
            unmapped_files.append(changed)
            continue
        for rule in matching_rules:
            specialty_tests.update(rule["specialtyTests"])
            contract_patterns.update(rule["contractConsumerGlobs"])
            if rule["publicContract"]:
                public_contract_files.append(changed)

    contract_consumers = sorted(
        set(_files_matching(repo, contract_patterns)) - set(changed_files)
    )
    result = {
        "changedFiles": changed_files,
        "closureFiles": sorted(
            set(changed_files) | set(direct_consumers) | set(contract_consumers)
        ),
        "contractConsumers": contract_consumers,
        "directConsumers": direct_consumers,
        "publicContractFiles": sorted(set(public_contract_files)),
        "schemaVersion": SCHEMA_VERSION,
        "specialtyTests": sorted(specialty_tests),
        "specialtyTestResources": [
            {
                "command": command,
                "resource": _resource_class_for_command(policy, command),
            }
            for command in sorted(specialty_tests)
        ],
        "unmappedFiles": unmapped_files,
    }
    sys.stdout.buffer.write(_json_bytes(result))
    return 0


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


@contextmanager
def _exclusive_file_lock(state_path: Path, label: str) -> Iterable[None]:
    lock_path = state_path.with_name(f"{state_path.name}.lock")
    flags = os.O_CREAT | os.O_RDWR
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(lock_path, flags, 0o600)
    except OSError as error:
        raise WorkflowError(f"{label} lock cannot be opened: {lock_path}") from error
    locked = False
    try:
        lock_stat = os.fstat(descriptor)
        if not stat.S_ISREG(lock_stat.st_mode) or lock_stat.st_nlink != 1:
            raise WorkflowError(f"{label} lock path is not a private file: {lock_path}")
        try:
            fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise WorkflowConflict(f"{label} BUSY: lock held for {state_path}") from error
        locked = True
        os.ftruncate(descriptor, 0)
        os.write(descriptor, f"{os.getpid()}\n".encode("ascii"))
        os.fsync(descriptor)
        yield
    finally:
        if locked:
            fcntl.flock(descriptor, fcntl.LOCK_UN)
        if descriptor >= 0:
            os.close(descriptor)


def _validate_queue_state(document: Any, path: Path) -> Dict[str, Any]:
    if not isinstance(document, dict) or set(document) != {
        "activeTask",
        "schemaVersion",
        "tasks",
    }:
        raise WorkflowError(f"queue state has missing or unknown fields: {path}")
    if document["schemaVersion"] != SCHEMA_VERSION:
        raise WorkflowError(f"unsupported queue schemaVersion: {path}")
    if document["activeTask"] is not None and not isinstance(
        document["activeTask"], str
    ):
        raise WorkflowError(f"queue activeTask is invalid: {path}")
    if not isinstance(document["tasks"], dict):
        raise WorkflowError(f"queue tasks must be an object: {path}")
    valid_states = {"writing", "testing", "review_wait", "blocked", "done"}
    if not all(
        isinstance(task, str)
        and task
        and isinstance(state, str)
        and state in valid_states
        for task, state in document["tasks"].items()
    ):
        raise WorkflowError(f"queue task state is invalid: {path}")
    active = document["activeTask"]
    non_terminal = sorted(
        task
        for task, state in document["tasks"].items()
        if state not in {"blocked", "done"}
    )
    if len(non_terminal) > 1:
        raise WorkflowError(f"queue has multiple non-terminal tasks: {path}")
    expected_active = non_terminal[0] if non_terminal else None
    if active != expected_active:
        raise WorkflowError(f"queue activeTask does not match task states: {path}")
    if active is not None and active not in document["tasks"]:
        raise WorkflowError(f"queue activeTask is missing from tasks: {path}")
    if active is not None and document["tasks"][active] in {"blocked", "done"}:
        raise WorkflowError(f"queue activeTask is not active: {path}")
    return document


def _load_queue_state(path: Path) -> Dict[str, Any]:
    return _validate_queue_state(_load_json(path), path)


def _queue_init(arguments: argparse.Namespace) -> int:
    state_path = _mutable_state_path(arguments.state)
    state_path.parent.mkdir(parents=True, exist_ok=True)
    with _exclusive_file_lock(state_path, "QUEUE"):
        if state_path.exists():
            raise WorkflowConflict(f"QUEUE EXISTS: {state_path}")
        _atomic_json_write(
            state_path,
            {"activeTask": None, "schemaVersion": SCHEMA_VERSION, "tasks": {}},
        )
    print(f"QUEUE INITIALIZED {state_path}")
    return 0


def _queue_acquire(arguments: argparse.Namespace) -> int:
    state_path = _mutable_state_path(arguments.state)
    if not arguments.task:
        raise WorkflowError("queue task must be non-empty")
    with _exclusive_file_lock(state_path, "QUEUE"):
        state = _load_queue_state(state_path)
        if state["activeTask"] is not None:
            raise WorkflowConflict(
                f"QUEUE DENIED: active {state['activeTask']}"
            )
        prior = state["tasks"].get(arguments.task)
        if prior == "done":
            raise WorkflowConflict(f"QUEUE DENIED: task {arguments.task} is done")
        if prior not in {None, "blocked"}:
            raise WorkflowError(
                f"task {arguments.task} cannot acquire from state {prior}"
            )
        state["activeTask"] = arguments.task
        state["tasks"][arguments.task] = "writing"
        _atomic_json_write(state_path, state)
    print(f"QUEUE ACQUIRED {arguments.task}")
    return 0


_QUEUE_TRANSITIONS = {
    "writing": {"testing", "blocked"},
    "testing": {"review_wait", "blocked"},
    "review_wait": {"writing", "done", "blocked"},
    "blocked": {"writing", "done"},
    "done": set(),
}


def _queue_transition(arguments: argparse.Namespace) -> int:
    state_path = _mutable_state_path(arguments.state)
    with _exclusive_file_lock(state_path, "QUEUE"):
        state = _load_queue_state(state_path)
        current = state["tasks"].get(arguments.task)
        if current is None:
            raise WorkflowError(f"unknown queue task: {arguments.task}")
        if arguments.to not in _QUEUE_TRANSITIONS[current]:
            raise WorkflowError(
                f"invalid transition for {arguments.task}: {current} -> {arguments.to}"
            )
        active = state["activeTask"]
        if current != "blocked" and active != arguments.task:
            raise WorkflowConflict(
                f"QUEUE DENIED: task {arguments.task} is not active"
            )
        if current == "blocked" and arguments.to == "writing":
            if active is not None:
                raise WorkflowConflict(f"QUEUE DENIED: active {active}")
            state["activeTask"] = arguments.task
        if arguments.to in {"blocked", "done"} and active == arguments.task:
            state["activeTask"] = None
        state["tasks"][arguments.task] = arguments.to
        _atomic_json_write(state_path, state)
    print(f"QUEUE TRANSITION {arguments.task}: {current} -> {arguments.to}")
    return 0


def _queue_status(arguments: argparse.Namespace) -> int:
    state = _load_queue_state(_mutable_state_path(arguments.state))
    sys.stdout.buffer.write(_json_bytes(state))
    return 0


_RESOURCE_ORDER = ("static-parallel", "dotnet-serial", "fixture-exclusive")


def _classify(arguments: argparse.Namespace) -> int:
    policy = _load_policy(arguments.policy)
    groups = {
        resource_class: {"commands": [], "paths": []}
        for resource_class in _RESOURCE_ORDER
    }
    for relative in sorted(set(arguments.path)):
        resource_class = _resource_class_for_path(policy, relative)
        groups[resource_class]["paths"].append(relative)
    for command in sorted(set(arguments.test_command)):
        resource_class = _resource_class_for_command(policy, command)
        groups[resource_class]["commands"].append(command)
    batches = [
        {
            "class": resource_class,
            "commands": groups[resource_class]["commands"],
            "parallel": resource_class == "static-parallel",
            "paths": groups[resource_class]["paths"],
        }
        for resource_class in _RESOURCE_ORDER
        if groups[resource_class]["commands"] or groups[resource_class]["paths"]
    ]
    result = {
        "executionBatches": batches,
        "groups": groups,
        "schemaVersion": SCHEMA_VERSION,
    }
    sys.stdout.buffer.write(_json_bytes(result))
    return 0


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


def _add_cache_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--repo", default=".")
    parser.add_argument("--record", required=True)
    parser.add_argument("--base", required=True)
    parser.add_argument("--command", required=True)
    parser.add_argument("--sdk-version")
    parser.add_argument("--env", action="append", default=[])
    parser.add_argument("--path", action="append", default=[], required=True)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command_group", required=True)

    cache = commands.add_parser("cache")
    cache_commands = cache.add_subparsers(dest="cache_command", required=True)

    record = cache_commands.add_parser("record")
    _add_cache_arguments(record)
    record.add_argument("--result", choices=("passed", "failed"), required=True)
    record.add_argument("--exit-code", type=int, required=True)
    record.add_argument("--duration-ms", type=int, required=True)
    record.set_defaults(handler=_cache_record)

    check = cache_commands.add_parser("check")
    _add_cache_arguments(check)
    check.set_defaults(handler=_cache_check)

    closure = commands.add_parser("closure")
    closure.add_argument("--repo", default=".")
    closure.add_argument("--policy", required=True)
    closure.add_argument("--changed", action="append", required=True)
    closure.set_defaults(handler=_closure)

    review_scope = commands.add_parser("review-scope")
    review_scope.add_argument("--evidence", action="append", required=True)
    review_scope.add_argument("--findings", required=True)
    review_scope.add_argument("--changed", action="append", required=True)
    review_scope.set_defaults(handler=_review_scope)

    queue = commands.add_parser("queue")
    queue_commands = queue.add_subparsers(dest="queue_command", required=True)

    queue_init = queue_commands.add_parser("init")
    queue_init.add_argument("--state", required=True)
    queue_init.set_defaults(handler=_queue_init)

    queue_acquire = queue_commands.add_parser("acquire")
    queue_acquire.add_argument("--state", required=True)
    queue_acquire.add_argument("--task", required=True)
    queue_acquire.set_defaults(handler=_queue_acquire)

    queue_transition = queue_commands.add_parser("transition")
    queue_transition.add_argument("--state", required=True)
    queue_transition.add_argument("--task", required=True)
    queue_transition.add_argument(
        "--to",
        choices=("writing", "testing", "review_wait", "blocked", "done"),
        required=True,
    )
    queue_transition.set_defaults(handler=_queue_transition)

    queue_status = queue_commands.add_parser("status")
    queue_status.add_argument("--state", required=True)
    queue_status.set_defaults(handler=_queue_status)

    classify = commands.add_parser("classify")
    classify.add_argument("--policy", required=True)
    classify.add_argument("--path", action="append", default=[])
    classify.add_argument("--test-command", action="append", default=[])
    classify.set_defaults(handler=_classify)

    metrics = commands.add_parser("metrics")
    metrics_commands = metrics.add_subparsers(dest="metrics_command", required=True)

    metrics_add = metrics_commands.add_parser("add")
    metrics_add.add_argument("--state", required=True)
    metrics_add.add_argument("--task", required=True)
    metrics_add.add_argument(
        "--phase",
        choices=("implementation", "test", "review", "idle"),
        required=True,
    )
    metrics_add.add_argument("--duration-ms", type=int, required=True)
    metrics_add.add_argument(
        "--cache-status", choices=("hit", "miss", "none"), required=True
    )
    metrics_add.add_argument("--command-label")
    metrics_add.add_argument("--rerun", action="store_true")
    metrics_add.add_argument("--conflict", action="store_true")
    metrics_add.add_argument(
        "--status", choices=("completed", "blocked"), required=True
    )
    metrics_add.set_defaults(handler=_metrics_add)

    metrics_report = metrics_commands.add_parser("report")
    metrics_report.add_argument("--state", required=True)
    metrics_report.set_defaults(handler=_metrics_report)

    return parser


def main() -> int:
    parser = _build_parser()
    arguments = parser.parse_args()
    try:
        return arguments.handler(arguments)
    except WorkflowConflict as error:
        print(f"codex-workflow: {error}", file=sys.stderr)
        return 1
    except WorkflowError as error:
        print(f"codex-workflow: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
