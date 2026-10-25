#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


SCRIPT = Path(__file__).with_name("security-layer.py")
FILES = (
    "src/Bukit-Core/Bukit.Engine/BuildTransaction.cs",
    "src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs",
    "src/Bukit-Core/Bukit.Engine/AssetOutputPlan.cs",
)


class SecurityLayerTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="bukit-security-coverage-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.report_count = 0

    def covered_classes(self):
        return [
            (path, Path(path).stem, [(number, 1) for number in range(1, 21)])
            for path in FILES
        ]

    def report(self, classes, sources=None):
        root = ET.Element("coverage", {"line-rate": "1"})
        source_nodes = ET.SubElement(root, "sources")
        for source in [self.repo] if sources is None else sources:
            ET.SubElement(source_nodes, "source").text = str(source)
        package = ET.SubElement(ET.SubElement(root, "packages"), "package")
        class_nodes = ET.SubElement(package, "classes")
        for filename, name, lines in classes:
            node = ET.SubElement(class_nodes, "class", {
                "filename": str(filename), "name": name, "line-rate": "1",
            })
            line_nodes = ET.SubElement(node, "lines")
            for number, hits in lines:
                ET.SubElement(line_nodes, "line", {
                    "number": str(number), "hits": str(hits),
                })
        self.report_count += 1
        path = self.root / f"coverage-{self.report_count}.xml"
        ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
        return path

    def run_checker(self, reports, explicit_repo=True):
        args = [sys.executable, "-B", str(SCRIPT)]
        if explicit_repo:
            args.extend(["--repo", str(self.repo)])
        return subprocess.run(
            [*args, *map(str, reports)],
            cwd=self.root if explicit_repo else self.repo,
            text=True, capture_output=True, timeout=10,
        )

    def assert_rows(self, result, expected, exit_code=0):
        self.assertEqual(result.returncode, exit_code, result.stderr or result.stdout)
        self.assertEqual(result.stderr, "")
        summary = json.loads(result.stdout)
        self.assertEqual(summary["minimum"], 95)
        self.assertEqual(len(summary["files"]), 3)
        self.assertEqual({
            row["file"]: (row["covered"], row["total"], row["passed"])
            for row in summary["files"]
        }, expected)

    def test_normal_with_explicit_or_current_repository(self):
        classes = self.covered_classes()
        classes[0] = (FILES[0], "BuildTransaction", [(7, 2), (42, 1), (120, 4)])
        report = self.report(classes)
        expected = {path: (20, 20, True) for path in FILES}
        expected[FILES[0]] = (3, 3, True)
        for explicit_repo in (True, False):
            with self.subTest(explicit_repo=explicit_repo):
                self.assert_rows(self.run_checker([report], explicit_repo), expected)

    def test_missing_file_fails_independently_of_same_basename(self):
        for missing in FILES:
            with self.subTest(missing=missing):
                classes = self.covered_classes()
                classes = [
                    (path.replace("Bukit.Engine", "Other.Project") if path == missing else path, name, lines)
                    for path, name, lines in classes
                ]
                expected = {path: (20, 20, True) for path in FILES}
                expected[missing] = (0, 0, False)
                self.assert_rows(self.run_checker([self.report(classes)]), expected, 1)

    def test_zero_denominator_fails_independently(self):
        for empty in FILES:
            with self.subTest(empty=empty):
                classes = [
                    (path, name, [] if path == empty else lines)
                    for path, name, lines in self.covered_classes()
                ]
                expected = {path: (20, 20, True) for path in FILES}
                expected[empty] = (0, 0, False)
                self.assert_rows(self.run_checker([self.report(classes)]), expected, 1)

    def test_undercovered_file_cannot_hide_in_passing_aggregate(self):
        for undercovered in FILES:
            with self.subTest(undercovered=undercovered):
                classes = [
                    (path, name, [(1, 0)] if path == undercovered else lines)
                    for path, name, lines in self.covered_classes()
                ]
                expected = {path: (20, 20, True) for path in FILES}
                expected[undercovered] = (0, 1, False)
                self.assert_rows(self.run_checker([self.report(classes)]), expected, 1)

    def test_duplicate_paths_merge_nested_and_async_source_lines(self):
        classes = self.covered_classes()[1:] + [
            (FILES[0], "BuildTransaction", [(10, 0), (20, 1)]),
            (f"./{FILES[0]}", "BuildTransaction/Nested", [(10, 1), (20, 0), (40, 2)]),
            (self.repo / "src/Bukit-Core/Bukit.Engine/../Bukit.Engine/BuildTransaction.cs",
             "BuildTransaction/<CommitAsync>d__42", [(20, 0), (30, 1)]),
        ]
        report = self.report(classes, sources=[self.repo, self.repo / "."])
        expected = {path: (20, 20, True) for path in FILES}
        expected[FILES[0]] = (4, 4, True)
        self.assert_rows(self.run_checker([report]), expected)

    def test_any_report_hit_covers_line_in_either_order(self):
        classes = self.covered_classes()
        first = self.report([
            (FILES[0], "BuildTransaction", [(10, 0), (20, 1)]), classes[1],
        ])
        second = self.report([
            (FILES[0], "BuildTransaction/<CommitAsync>d__42", [(10, 1), (20, 0), (30, 1)]),
            classes[2],
        ])
        expected = {path: (20, 20, True) for path in FILES}
        expected[FILES[0]] = (3, 3, True)
        for reports in ([first, second], [second, first, second]):
            with self.subTest(reports=reports):
                self.assert_rows(self.run_checker(reports), expected)

    def test_absolute_filenames_without_sources(self):
        classes = [
            (self.repo / path, name, lines)
            for path, name, lines in self.covered_classes()
        ]
        self.assert_rows(
            self.run_checker([self.report(classes, sources=[])]),
            {path: (20, 20, True) for path in FILES},
        )

    def test_exactly_95_percent_passes_each_file(self):
        classes = [
            (path, Path(path).stem, [(number, int(number <= 19)) for number in range(1, 21)])
            for path in FILES
        ]
        self.assert_rows(
            self.run_checker([self.report(classes)]),
            {path: (19, 20, True) for path in FILES},
        )

    def test_rounding_to_95_percent_does_not_pass(self):
        self.assertEqual(f"{1899 / 1999 * 100:.2f}", "95.00")
        classes = self.covered_classes()
        classes[0] = (FILES[0], "BuildTransaction", [
            (number, int(number <= 1899)) for number in range(1, 2000)
        ])
        expected = {path: (20, 20, True) for path in FILES}
        expected[FILES[0]] = (1899, 1999, False)
        self.assert_rows(self.run_checker([self.report(classes)]), expected, 1)

    def test_invalid_reports_fail_without_success_output(self):
        malformed = self.root / "malformed.xml"
        malformed.write_text("<coverage>", encoding="utf-8")
        bad_hits = self.report([(FILES[0], "BuildTransaction", [(1, "invalid")])])
        for report in (self.root / "missing.xml", malformed, bad_hits):
            with self.subTest(report=report):
                result = self.run_checker([report])
                self.assertEqual(result.returncode, 2)
                self.assertEqual(result.stdout, "")
                self.assertIn("ERROR:", result.stderr)

    def test_requires_at_least_one_report(self):
        result = self.run_checker([])
        self.assertEqual(result.returncode, 2)
        self.assertIn("usage:", result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
