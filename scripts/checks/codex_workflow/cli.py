"""Deterministic helpers for Bukit's high-speed Codex workflow."""

from __future__ import annotations

import argparse
import sys

from .cache import _cache_check, _cache_record
from .classification import _classify
from .closure import _closure
from .common import WorkflowConflict, WorkflowError
from .metrics import _metrics_add, _metrics_report
from .queue import _queue_acquire, _queue_init, _queue_status, _queue_transition
from .review import _review_scope

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
