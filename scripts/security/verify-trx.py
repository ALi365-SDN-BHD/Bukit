#!/usr/bin/env python3
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def verify_trx(path: Path, selectors: list[str]) -> None:
    root = ET.parse(path).getroot()
    counters = root.find(".//{*}ResultSummary/{*}Counters")
    if counters is None:
        raise ValueError("TRX counters are missing")
    names = ("total", "executed", "passed", "failed", "notExecuted")
    values = {name: int(counters.attrib.get(name, "0")) for name in names}
    if values["total"] <= 0:
        raise ValueError("TRX contains zero tests")
    if not (values["executed"] == values["passed"] == values["total"]
            and values["failed"] == values["notExecuted"] == 0):
        raise ValueError(f"TRX tests were not all executed and passed: {values}")
    methods: dict[str, str] = {}
    for unit in root.findall(".//{*}UnitTest"):
        method = unit.find("./{*}TestMethod")
        if method is not None:
            methods[unit.attrib["id"]] = (
                f'{method.attrib.get("className", "")}.{method.attrib.get("name", "")}'
            )
    executed = [
        methods.get(result.attrib.get("testId", ""), result.attrib.get("testName", ""))
        for result in root.findall(".//{*}UnitTestResult")
        if result.attrib.get("outcome") == "Passed"
    ]
    missing = [
        selector for selector in selectors
        if selector.removeprefix("FullyQualifiedName~") not in "\n".join(executed)
    ]
    if missing:
        raise ValueError(f"security selectors have no executed result: {missing}")


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(
            "usage: verify-trx.py <trx-path> <FullyQualifiedName~selector>...",
            file=sys.stderr,
        )
        return 2
    try:
        verify_trx(Path(argv[1]), argv[2:])
    except (ET.ParseError, OSError, ValueError) as error:
        print(f"security TRX validation failed: {error}", file=sys.stderr)
        return 1
    print(f"security TRX validation OK: {argv[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
