"""Schema vocabulary and binding checks for the Core report inventory (stdlib only)."""

import json
from pathlib import Path
import re
from urllib.parse import unquote


KEYWORDS = {
    "$schema", "$id", "$defs", "$ref", "title", "description", "$comment", "default", "examples",
    "type", "const", "enum", "properties", "required", "additionalProperties", "items", "minimum", "maximum",
    "minLength", "pattern", "format", "minItems", "maxItems", "uniqueItems", "oneOf",
}
CURRENT_IDS = set("build-report routes assets incremental-manifest security-report release-bundle-checksums "
                  "artifact-manifest build-manifest-digest publish-url-snapshot seo-report publish-audit-report "
                  "seo-route-map geo-report analytics-report seo-insights-report seo-question-insights-report "
                  "generative-citation-report external-authority-report cli-error plugin-execution-report "
                  "agent-manifest metrics search-index-index theme-catalog".split())


def require(condition, message):
    if not condition:
        raise ValueError(message)


def source(root, path):
    require(isinstance(path, str) and not Path(path).is_absolute() and ".." not in Path(path).parts,
            "Source/schema paths must be repository-relative")
    target = root / path
    require(target.is_file(), "Missing file: " + path)
    return target.read_text(encoding="utf-8")


def symbol(root, binding):
    text = source(root, binding["source"])
    # shortcut: lexical C# declaration checks, not semantic parsing; use Roslyn if bindings require aliases or computed constants.
    parts = binding["symbol"].split(".")
    if len(parts) > 1 or parts[0].endswith("Tests"):
        require(re.search(r"\b(?:class|record|struct)\s+" + re.escape(parts[0]) + r"\b", text),
                "Missing type symbol: " + binding["symbol"])
    if len(parts) > 1 or not parts[0].endswith("Tests"):
        require(re.search(r"\b" + re.escape(parts[-1]) + r"\s*\(", text), "Missing method symbol: " + binding["symbol"])
    return text


def declaration(root, binding, prefix, expected):
    text = source(root, binding.get(prefix + "Source", binding["source"]))
    name = binding.get(prefix + "Symbol")
    if name:
        match = re.search(r"\b" + re.escape(name) + r'\s*=\s*("(?:[^"\\]|\\.)*"|-?\d+)', text)
        require(match is not None, "Missing constant: " + name)
        actual = json.loads(match.group(1))
        require(type(actual) is type(expected) and actual == expected, "Stale declaration: " + name)
    else:
        expression = binding[prefix + "Expression"]
        require(expression in text and json.dumps(expected) in expression, "Stale declaration expression: " + expression)


def vocabulary(schema, root=None):
    root = schema if root is None else root
    if isinstance(schema, bool):
        return
    require(isinstance(schema, dict), "Schema must be an object or boolean")
    for key, value in schema.items():
        require(key in KEYWORDS, "Unsupported schema keyword: " + key)
        if key == "$ref":
            require(value.startswith("#/"), "Unsupported external reference: " + value)
            target = root
            for segment in value[2:].split("/"):
                segment = unquote(segment).replace("~1", "/").replace("~0", "~")
                require(isinstance(target, dict) and segment in target, "Unresolved reference: " + value)
                target = target[segment]
            require(isinstance(target, (dict, bool)), "Reference is not a schema: " + value)
        elif key in ("properties", "$defs"):
            for child in value.values():
                vocabulary(child, root)
        elif key in ("items", "additionalProperties"):
            vocabulary(value, root)
        elif key == "oneOf":
            for child in value:
                vocabulary(child, root)
        elif key == "format":
            require(value in ("date", "date-time", "uri"), "Unsupported schema format: " + value)
        elif key == "type":
            require(set(value if isinstance(value, list) else [value]) <=
                    {"object", "array", "string", "number", "integer", "boolean", "null"}, "Unsupported schema type")


def check(root, inventory):
    require(inventory.get("inventoryVersion") == 1, "Unsupported inventory version")
    records = inventory["records"]
    require(records, "Empty inventory")
    ids, local_paths, schema_ids, artifacts = set(), set(), set(), set()
    for record in records:
        identity = record["id"]
        require(identity not in ids, "Duplicate record: " + identity)
        ids.add(identity)
        require(record.get("classification") and record.get("artifactPaths") and record.get("tests"), "Incomplete record: " + identity)
        artifact_sources = [symbol(root, record["producer"])]
        require(record["entrypoints"] or record["classification"] == "public-api-only", "Missing entry point: " + identity)
        for binding in record["entrypoints"]:
            artifact_sources.append(symbol(root, binding))
        for binding in record["tests"]:
            symbol(root, binding)
        for path in record["artifactPaths"]:
            require(path not in artifacts or path == "{cliOut}", "Duplicate artifact path: " + path)
            artifacts.add(path)
            name = path.rsplit("/", 1)[-1]
            if name.endswith(".json") and "{" not in name:
                require(any('"' + name + '"' in text for text in artifact_sources), "Artifact filename is not bound to producer/entrypoint: " + path)
        declared, local, status = record["declaredSchemaId"], record["localSchema"], record["schemaStatus"]
        version = record["versionBinding"]
        if declared:
            require(declared not in schema_ids, "Duplicate schema ID: " + declared)
            schema_ids.add(declared)
            declaration(root, record["declaration"], "schema", declared)
        if version:
            declaration(root, record["declaration"], "version", version["value"])
        if status == "declared-local":
            require(declared and local, "Missing declared/local binding: " + identity)
        elif status == "explicit-local-binding-no-payload-declaration":
            require(declared is None and version is None and local, "Invalid explicit local binding")
            require(Path(local).name in source(root, record["tests"][0]["source"]), "Missing explicit test/schema association")
        elif status == "declared-but-local-schema-missing":
            require(declared and local is None and record.get("knownGap"), "Missing schema must remain an explicit gap")
            expected = declared.rsplit("/", 1)[-1].replace(".json", ".schema.json")
            require(not (root / "docs/schemas" / expected).exists(), "Missing-schema status is stale")
        elif status == "no-declaration-no-local-schema":
            require(declared is None and local is None, "Invalid no-schema binding")
        else:
            raise ValueError("Unknown schema status: " + status)
        if not local:
            continue
        require(local not in local_paths, "Duplicate local schema: " + local)
        local_paths.add(local)
        schema = json.loads(source(root, local))
        require(schema.get("$id") == (declared or record["localSchemaId"]), "Stale schema ID: " + identity)
        if declared:
            require(schema["properties"]["schema"].get("const") == declared, "Stale schema const: " + identity)
        if version:
            actual = schema["properties"][version["property"]].get("const")
            require(type(actual) is type(version["value"]) and actual == version["value"], "Stale schema version: " + identity)
        vocabulary(schema)
    return len(records), len(local_paths)
