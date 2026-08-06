"""Repository and verification-fingerprint helpers."""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import subprocess
from typing import Any, Dict, Iterable

from .common import WorkflowError, _lexical_absolute

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

