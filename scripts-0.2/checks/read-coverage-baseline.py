#!/usr/bin/env python3
import json
import sys
from pathlib import Path


def usage() -> None:
    print(
        "Usage: read-coverage-baseline.py <coverage-baselines.json> <module> <key>",
        file=sys.stderr,
    )


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        usage()
        return 2

    path = Path(argv[1])
    module = argv[2]
    key = argv[3]

    if not path.exists():
        return 0

    data = json.loads(path.read_text(encoding="utf-8"))
    value = data.get(module, {}).get(key)
    if value is None:
        return 0

    if isinstance(value, bool):
        print("true" if value else "false")
    else:
        print(value)

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
