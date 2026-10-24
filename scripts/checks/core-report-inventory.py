#!/usr/bin/env python3
"""Check current report bindings and the bounded local schema vocabulary (stdlib only)."""

import argparse
import copy
import json
from pathlib import Path
import re
import tempfile
import unittest
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


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="bukit-inventory-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "writer.cs").write_text('class Writer { const string Schema = "urn:test"; const string Version = "2.0"; void Write() { Save("report.json"); } }')
        (self.root / "test.cs").write_text("class ReportTests { void Writes() {} }")
        self.schema = {"$id": "urn:test", "type": "object", "properties": {"schema": {"const": "urn:test"}, "schemaVersion": {"const": "2.0"}},
                       "$defs": {"value": {"type": "integer", "minimum": 0}}, "additionalProperties": {"$ref": "#/$defs/value"}}
        self.write_schema()
        self.inventory = {"inventoryVersion": 1, "records": [{"id": "report", "classification": "build-report",
            "producer": {"source": "writer.cs", "symbol": "Writer.Write"},
            "entrypoints": [{"source": "writer.cs", "symbol": "Writer.Write"}], "artifactPaths": ["{outputRoot}/report.json"],
            "declaredSchemaId": "urn:test", "localSchema": "schema.json", "schemaStatus": "declared-local",
            "versionBinding": {"property": "schemaVersion", "value": "2.0"},
            "declaration": {"source": "writer.cs", "schemaSymbol": "Schema", "versionSymbol": "Version"},
            "tests": [{"source": "test.cs", "symbol": "ReportTests.Writes"}]}]}

    def write_schema(self):
        (self.root / "schema.json").write_text(json.dumps(self.schema))

    def test_valid_and_missing_files(self):
        self.assertEqual((1, 1), check(self.root, self.inventory))
        for name in ("writer.cs", "schema.json", "test.cs"):
            with self.subTest(name=name):
                path = self.root / name
                text = path.read_text()
                path.unlink()
                with self.assertRaisesRegex(ValueError, "Missing file"):
                    check(self.root, self.inventory)
                path.write_text(text)

    def test_stale_symbols_versions_paths_and_duplicates(self):
        for field, value in (("producer", {"source": "writer.cs", "symbol": "Writer.Missing"}),
                             ("tests", [{"source": "test.cs", "symbol": "ReportTests.Missing"}]),
                             ("artifactPaths", ["{outputRoot}/missing.json"]),
                             ("declaredSchemaId", "urn:stale"),
                             ("versionBinding", {"property": "schemaVersion", "value": "1.0"})):
            with self.subTest(field=field):
                changed = copy.deepcopy(self.inventory)
                changed["records"][0][field] = value
                with self.assertRaises(ValueError):
                    check(self.root, changed)
        changed = copy.deepcopy(self.inventory)
        changed["records"] *= 2
        with self.assertRaisesRegex(ValueError, "Duplicate record"):
            check(self.root, changed)

    def test_schema_id_const_version_keyword_and_reference(self):
        original = copy.deepcopy(self.schema)
        for mutation in (lambda s: s.update({"$id": "urn:wrong"}),
                         lambda s: s["properties"]["schema"].update(const="urn:wrong"),
                         lambda s: s["properties"]["schemaVersion"].update(const=2),
                         lambda s: s["$defs"]["value"].update(maxLength=4),
                         lambda s: s.update(additionalProperties={"$ref": "#/$defs/missing"}),
                         lambda s: s.update(additionalProperties={"$ref": "https://example.test/schema"}),
                         lambda s: s["$defs"]["value"].update(format="email")):
            self.schema = copy.deepcopy(original)
            mutation(self.schema)
            self.write_schema()
            with self.assertRaises(ValueError):
                check(self.root, self.inventory)

    def test_pointer_escaping_and_annotations(self):
        vocabulary({"$defs": {"a/b~c": {"type": "integer"}}, "$ref": "#/$defs/a~1b~0c", "examples": [{"notAKeyword": 1}]})

    def test_artifact_path_can_be_supplied_by_entrypoint(self):
        path = self.root / "writer.cs"
        path.write_text(path.read_text().replace('Save("report.json");', ""))
        (self.root / "entry.cs").write_text('class Entry { void Run() { Write("report.json"); } }')
        self.inventory["records"][0]["entrypoints"] = [{"source": "entry.cs", "symbol": "Entry.Run"}]
        self.assertEqual((1, 1), check(self.root, self.inventory))
        (self.root / "entry.cs").write_text('class Entry { void Run() { Write("wrong.json"); } }')
        with self.assertRaisesRegex(ValueError, "Artifact filename"):
            check(self.root, self.inventory)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(InventoryTests))
        if not result.wasSuccessful():
            return 1
    root = Path(__file__).resolve().parents[2]
    inventory = json.loads(source(root, "docs/testing/core-report-inventory.json"))
    counts = check(root, inventory)
    require({record["id"] for record in inventory["records"]} == CURRENT_IDS and counts == (24, 20), "Current producer inventory changed; update its explicit coverage")
    analytics = next(record for record in inventory["records"] if record["id"] == "analytics-report")
    require(analytics["declaredSchemaId"] == "https://bukit.dev/schemas/analytics-report.v2.json" and analytics["versionBinding"]["value"] == "2.0", "Analytics must bind current v2")
    print("Core report inventory: 24 records, 20 local schemas; known contract gaps retained")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
