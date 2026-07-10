#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import os
import sys
import tempfile
from pathlib import Path


def lexical_path(raw: str, repo: Path) -> Path:
    path = Path(raw).expanduser()
    return Path(os.path.abspath(path if path.is_absolute() else repo / path))


def below(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
    except ValueError:
        return False
    return path != root


def dedicated_temp_path(path: Path, root: Path) -> bool:
    try:
        relative = path.relative_to(root)
    except ValueError:
        return False
    return bool(relative.parts) and relative.parts[0].startswith("bukit-coverage-")


def temp_roots() -> set[Path]:
    roots = {Path(tempfile.gettempdir()), Path("/tmp")}
    lexical = {Path(os.path.abspath(root)) for root in roots}
    return lexical | {root.resolve() for root in lexical}


def resolves_without_inner_symlink(path: Path, root: Path) -> bool:
    relative = path.relative_to(root)
    expected = root.resolve().joinpath(*relative.parts)
    return path.resolve() == expected


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: validate-output-root.py <output-root> <repo-root>", file=sys.stderr)
        return 2

    repo = Path(argv[2]).resolve()
    output_path = lexical_path(argv[1], repo)
    output = output_path.resolve()
    repo_coverage_path = repo / "TestResults" / "coverage"
    repo_coverage = repo_coverage_path.resolve()
    home = Path.home().resolve()

    if repo_coverage != repo_coverage_path:
        print("unsafe coverage root contains a symbolic link", file=sys.stderr)
        return 1
    if output_path == repo_coverage or below(output_path, repo_coverage):
        if output != output_path:
            print("unsafe coverage root contains a symbolic link", file=sys.stderr)
            return 1
        print(output)
        return 0
    if (
        output_path == repo
        or below(output_path, repo)
        or output == repo
        or below(output, repo)
        or output == home
        or below(output, home)
    ):
        print(f"unsafe coverage output directory: {argv[1]}", file=sys.stderr)
        return 1
    for root in temp_roots():
        if dedicated_temp_path(output_path, root):
            if not resolves_without_inner_symlink(output_path, root):
                print("unsafe coverage root contains a symbolic link", file=sys.stderr)
                return 1
            print(output)
            return 0

    print(f"unsafe coverage output directory: {argv[1]}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
