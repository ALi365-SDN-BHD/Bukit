"""Verification closure generation and resource-policy helpers."""

from __future__ import annotations

import argparse
from pathlib import Path
import re
import sys
from typing import Any, Dict, Iterable, List

from .common import SCHEMA_VERSION, WorkflowError, _json_bytes, _load_policy
from .repo import _matches, _relative_repo_path, _resolved_repo

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

