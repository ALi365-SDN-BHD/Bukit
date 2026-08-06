"""Single-writer queue coordination."""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import fcntl
import os
from pathlib import Path
import stat
import sys
from typing import Any, Dict, Iterable

from .common import (
    SCHEMA_VERSION,
    WorkflowConflict,
    WorkflowError,
    _atomic_json_write,
    _json_bytes,
    _load_json,
    _mutable_state_path,
)


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
