#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import json
import sys
from pathlib import Path

ALLOWED_TOP_LEVEL = {
    "$schema",
    "version",
    "scope",
    "metric",
    "sourceRoot",
    "minimums",
}
LEGACY_FIELDS = {"core", "cli", "importing", "labs", "blocking", "trackedOnly", "baseline"}


def fail(message: str) -> int:
    print(f"ERROR: {message}", file=sys.stderr)
    return 1


def number(value: object, path: str) -> float:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        raise ValueError(f"{path} must be a number")
    value = float(value)
    if value < 0 or value > 100:
        raise ValueError(f"{path} must be between 0 and 100")
    return value


def validate(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("policy must be a JSON object")

    extra = sorted(set(data) - ALLOWED_TOP_LEVEL)
    if extra:
        raise ValueError(f"unexpected top-level fields: {', '.join(extra)}")

    legacy = sorted(set(data) & LEGACY_FIELDS)
    if legacy:
        raise ValueError(f"legacy coverage fields are not allowed: {', '.join(legacy)}")

    for field in ("version", "scope", "metric", "sourceRoot", "minimums"):
        if field not in data:
            raise ValueError(f"missing required field: {field}")

    if data["version"] != "2.0.0":
        raise ValueError("version must be 2.0.0")
    if data["scope"] != "core":
        raise ValueError("scope must be core")
    if data["metric"] != "line":
        raise ValueError("metric must be line")
    if data["sourceRoot"] != "src/Bukit-Core":
        raise ValueError("sourceRoot must be src/Bukit-Core")

    minimums = data["minimums"]
    if not isinstance(minimums, dict):
        raise ValueError("minimums must be an object")
    if set(minimums) != {"overall", "projectFloor"}:
        raise ValueError("minimums must contain only overall and projectFloor")

    number(minimums["overall"], "minimums.overall")
    number(minimums["projectFloor"], "minimums.projectFloor")


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: validate-policy.py <coverage-baselines.json>", file=sys.stderr)
        return 2

    path = Path(argv[1])
    try:
        validate(path)
    except (OSError, json.JSONDecodeError, ValueError) as ex:
        return fail(str(ex))

    print(f"coverage policy OK: {path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
