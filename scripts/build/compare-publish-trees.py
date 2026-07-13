#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import sys
from pathlib import Path


def manifest(root: Path) -> dict[str, tuple[str, int, str]]:
    if root.is_symlink() or not root.is_dir():
        raise ValueError(f"publish root is not a directory: {root}")

    result: dict[str, tuple[str, int, str]] = {}
    for path in sorted(root.rglob("*")):
        rel = path.relative_to(root).as_posix()
        if path.is_symlink() or not (path.is_dir() or path.is_file()):
            raise ValueError(f"unsupported publish entry: {rel}")
        if path.is_dir():
            result[rel] = ("dir", 0, "")
        else:
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            result[rel] = ("file", path.stat().st_size, digest)
    return result


def compare(left: Path, right: Path) -> None:
    left_items, right_items = manifest(left), manifest(right)
    missing = sorted(left_items.keys() - right_items.keys())
    extra = sorted(right_items.keys() - left_items.keys())
    changed = sorted(
        name
        for name in left_items.keys() & right_items.keys()
        if left_items[name] != right_items[name]
    )
    if missing or extra or changed:
        raise ValueError(f"missing={missing} extra={extra} changed={changed}")


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: compare-publish-trees.py LEFT RIGHT", file=sys.stderr)
        return 2

    try:
        compare(Path(argv[1]), Path(argv[2]))
    except (ValueError, OSError) as error:
        print(f"publish trees differ: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
