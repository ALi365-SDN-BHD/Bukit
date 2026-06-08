#!/usr/bin/env python3
import json
import re
import sys
from pathlib import Path


def json_type(value):
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int) and not isinstance(value, bool):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return type(value).__name__


def resolve_ref(schema, ref):
    if not ref.startswith("#/"):
        raise ValueError(f"unsupported $ref '{ref}'")

    node = schema
    for raw_part in ref[2:].split("/"):
        part = raw_part.replace("~1", "/").replace("~0", "~")
        node = node[part]
    return node


def validate(value, node, root_schema, path, errors):
    if "$ref" in node:
        validate(value, resolve_ref(root_schema, node["$ref"]), root_schema, path, errors)
        return

    if "const" in node and value != node["const"]:
        errors.append(f"{path}: expected const {node['const']!r}, got {value!r}")

    if "enum" in node and value not in node["enum"]:
        errors.append(f"{path}: expected one of {node['enum']!r}, got {value!r}")

    expected_type = node.get("type")
    if expected_type is not None:
        expected = expected_type if isinstance(expected_type, list) else [expected_type]
        actual = json_type(value)
        if actual == "integer" and "number" in expected:
            pass
        elif actual not in expected:
            errors.append(f"{path}: expected type {expected!r}, got {actual}")
            return

    if isinstance(value, (int, float)) and "minimum" in node and value < node["minimum"]:
        errors.append(f"{path}: expected >= {node['minimum']}, got {value}")

    if isinstance(value, str) and "pattern" in node and re.search(node["pattern"], value) is None:
        errors.append(f"{path}: expected pattern /{node['pattern']}/, got {value!r}")

    if isinstance(value, dict):
        required = node.get("required", [])
        for key in required:
            if key not in value:
                errors.append(f"{path}: missing required property '{key}'")

        properties = node.get("properties", {})
        for key, child in value.items():
            child_path = f"{path}.{key}"
            if key in properties:
                validate(child, properties[key], root_schema, child_path, errors)
                continue

            additional = node.get("additionalProperties", True)
            if additional is False:
                errors.append(f"{child_path}: additional property is not allowed")
            elif isinstance(additional, dict):
                validate(child, additional, root_schema, child_path, errors)

    if isinstance(value, list) and "items" in node:
        for index, item in enumerate(value):
            validate(item, node["items"], root_schema, f"{path}[{index}]", errors)


def main(argv):
    if len(argv) != 3:
        print("usage: validate-json-schema.py <schema.json> <document.json>", file=sys.stderr)
        return 2

    schema_path = Path(argv[1])
    document_path = Path(argv[2])
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    document = json.loads(document_path.read_text(encoding="utf-8"))
    errors = []
    validate(document, schema, schema, "$", errors)
    if errors:
        print(f"ERROR: {document_path} does not match {schema_path}", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
