#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import json
import sys
from pathlib import Path


def valid_project(project: str) -> bool:
    path = Path(project)
    if path.is_absolute() or len(path.parts) != 3 or path.parts[0] != "tests":
        return False
    if path.suffix != ".csproj" or path.parent.name != path.stem:
        return False
    try:
        path.resolve().relative_to(Path.cwd().resolve())
    except ValueError:
        return False
    return path.is_file()


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: matrix.py <projects.tsv>", file=sys.stderr)
        return 2

    rows = []
    names: set[str] = set()
    for raw in Path(argv[1]).read_text(encoding="utf-8").splitlines():
        if raw.count("\t") != 1:
            print(f"coverage project row must have two columns: {raw}", file=sys.stderr)
            return 1
        project, filter_value = raw.split("\t")
        name = Path(project).parent.name
        if not valid_project(project) or name in names:
            print(f"invalid or duplicate coverage project: {project}", file=sys.stderr)
            return 1
        names.add(name)
        rows.append({"project": project, "name": name, "filter": filter_value})

    if not rows:
        print("coverage project list is empty", file=sys.stderr)
        return 1
    print(json.dumps({"include": rows}, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
