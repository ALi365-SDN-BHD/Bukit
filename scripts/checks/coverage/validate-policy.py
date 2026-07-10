#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import json
import math
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
EXPECTED_SCHEMA_REF = "schemas/coverage-baselines.v2.json"
EXPECTED_SCHEMA_ID = "https://bukit.dev/schemas/coverage-baselines.v2.json"
EXPECTED_SCHEMA_DRAFT = "https://json-schema.org/draft/2020-12/schema"


def fail(message: str) -> int:
    print(f"ERROR: {message}", file=sys.stderr)
    return 1


def reject_constant(value: str) -> None:
    raise ValueError(f"invalid JSON constant: {value}")


def load_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"), parse_constant=reject_constant)


def number(value: object, path: str) -> float:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        raise ValueError(f"{path} must be a number")
    value = float(value)
    if not math.isfinite(value) or value < 0 or value > 100:
        raise ValueError(f"{path} must be between 0 and 100")
    return value


def validate_schema(path: Path, data: dict[str, object]) -> None:
    if data["$schema"] != EXPECTED_SCHEMA_REF:
        raise ValueError(f"$schema must be {EXPECTED_SCHEMA_REF}")
    schema = load_json((path.parent / EXPECTED_SCHEMA_REF).resolve())
    if not isinstance(schema, dict):
        raise ValueError("coverage schema must be an object")
    if schema.get("$schema") != EXPECTED_SCHEMA_DRAFT or schema.get("$id") != EXPECTED_SCHEMA_ID:
        raise ValueError("coverage schema identity is invalid")
    if schema.get("type") != "object" or schema.get("additionalProperties") is not False:
        raise ValueError("coverage schema root contract is invalid")
    if schema.get("required") != ["$schema", "version", "scope", "metric", "sourceRoot", "minimums"]:
        raise ValueError("coverage schema required fields are invalid")
    properties = schema.get("properties")
    if not isinstance(properties, dict) or set(properties) != ALLOWED_TOP_LEVEL:
        raise ValueError("coverage schema properties are invalid")
    if properties.get("$schema") != {"const": EXPECTED_SCHEMA_REF}:
        raise ValueError("coverage schema pointer contract is invalid")
    for field in ("version", "scope", "metric", "sourceRoot"):
        contract = properties.get(field)
        if contract != {"const": data[field]}:
            raise ValueError(f"coverage schema {field} contract does not match policy")
    number_contract = {"type": "number", "minimum": 0, "maximum": 100}
    expected_minimums = {
        "type": "object",
        "required": ["overall", "projectFloor"],
        "properties": {"overall": number_contract, "projectFloor": number_contract},
        "additionalProperties": False,
    }
    if properties.get("minimums") != expected_minimums:
        raise ValueError("coverage schema minimums contract is invalid")


def validate(path: Path) -> None:
    data = load_json(path)
    if not isinstance(data, dict):
        raise ValueError("policy must be a JSON object")

    extra = sorted(set(data) - ALLOWED_TOP_LEVEL)
    if extra:
        raise ValueError(f"unexpected top-level fields: {', '.join(extra)}")

    legacy = sorted(set(data) & LEGACY_FIELDS)
    if legacy:
        raise ValueError(f"legacy coverage fields are not allowed: {', '.join(legacy)}")

    for field in ("$schema", "version", "scope", "metric", "sourceRoot", "minimums"):
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
    validate_schema(path, data)

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
