#!/usr/bin/env python3
"""Require 95% executable source-line coverage for each Engine safety-layer file."""
from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from summarize import collect


FILES = (
    "src/Bukit-Core/Bukit.Engine/BuildTransaction.cs",
    "src/Bukit-Core/Bukit.Engine/OutputDirectoryCleaner.cs",
    "src/Bukit-Core/Bukit.Engine/AssetOutputPlan.cs",
)
MINIMUM = 95


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("coverage", nargs="+", type=Path, metavar="COBERTURA")
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="repository root (default: current directory)")
    args = parser.parse_args(argv)
    try:
        line_hits = collect(args.repo.resolve(), args.coverage)
    except (OSError, ET.ParseError, KeyError, ValueError, OverflowError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 2

    rows = []
    for source in FILES:
        values = [hits for (path, _), hits in line_hits.items() if path == source]
        covered = sum(1 for hits in values if hits > 0)
        total = len(values)
        rows.append({
            "file": source,
            "covered": covered,
            "total": total,
            "passed": total > 0 and covered * 100 >= total * MINIMUM,
        })
    print(json.dumps({"minimum": MINIMUM, "files": rows}, indent=2))
    return 0 if all(row["passed"] for row in rows) else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
