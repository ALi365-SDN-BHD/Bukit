"""Verification resource classification."""

from __future__ import annotations

import argparse
import sys

from .closure import _resource_class_for_command, _resource_class_for_path
from .common import SCHEMA_VERSION, _json_bytes, _load_policy

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

