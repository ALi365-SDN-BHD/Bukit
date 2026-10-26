#!/usr/bin/env python3
"""Check current report bindings and the bounded local schema vocabulary (stdlib only)."""

import argparse
import copy
import json
from pathlib import Path
import tempfile
import unittest

from core_report_inventory_lib import CURRENT_IDS, check, require, source, vocabulary


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
