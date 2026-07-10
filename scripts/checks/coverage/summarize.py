#!/bin/sh
"exec" "python3" "$0" "$@"
from __future__ import annotations

import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

CORE_SOURCE_ROOT = "src/Bukit-Core"


def pct(covered: int, total: int) -> float:
    return round((covered / total) * 100, 2) if total else 0.0


def below_threshold(covered: int, total: int, floor: float) -> bool:
    value = (covered / total) * 100 if total else 0.0
    return value < floor


def rel_source(repo: Path, sources: list[str], filename: str) -> str | None:
    raw = Path(filename)
    candidates = [raw] if raw.is_absolute() else []
    candidates.extend(Path(source) / filename for source in sources)
    for candidate in candidates:
        try:
            rel = candidate.resolve().relative_to(repo)
        except (OSError, ValueError):
            continue
        if len(rel.parts) >= 3 and rel.parts[0] == "src" and rel.parts[1] == "Bukit-Core":
            return rel.as_posix()
    return None


def collect(repo: Path, coverage_files: list[Path]) -> dict[tuple[str, int], int]:
    hits: dict[tuple[str, int], int] = {}
    for coverage_file in coverage_files:
        root = ET.parse(coverage_file).getroot()
        sources = [node.text or "" for node in root.findall("./sources/source")]
        for class_node in root.findall(".//class"):
            source = rel_source(repo, sources, class_node.attrib.get("filename", ""))
            if source is None:
                continue
            for line_node in class_node.findall("./lines/line"):
                key = (source, int(line_node.attrib["number"]))
                value = int(float(line_node.attrib.get("hits", "0")))
                hits[key] = hits.get(key, 0) + value
    return hits


def expected_projects(repo: Path, source_root: str) -> set[str]:
    root = repo / source_root
    return {
        path.name
        for path in root.iterdir()
        if path.is_dir() and (path / f"{path.name}.csproj").exists()
    }


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print("usage: summarize.py <policy.json> <output-root> <coverage-files.txt>", file=sys.stderr)
        return 2

    repo = Path.cwd().resolve()
    policy = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
    output_root = Path(argv[2])
    coverage_files = [
        Path(line)
        for line in Path(argv[3]).read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    line_hits = collect(repo, coverage_files)

    by_project: dict[str, list[int]] = {}
    for (source, _), hits in line_hits.items():
        by_project.setdefault(source.split("/")[2], []).append(hits)

    rows = []
    for project in sorted(by_project):
        values = by_project[project]
        covered = sum(1 for value in values if value > 0)
        total = len(values)
        rows.append({"project": project, "covered": covered, "total": total, "line": pct(covered, total)})

    covered = sum(1 for value in line_hits.values() if value > 0)
    total = len(line_hits)
    overall = pct(covered, total)
    minimums = policy["minimums"]
    overall_floor = float(minimums["overall"])
    project_floor = float(minimums["projectFloor"])
    missing = sorted(expected_projects(repo, str(policy["sourceRoot"])) - {row["project"] for row in rows})
    failures = [f"missing coverage for {project}" for project in missing]
    if below_threshold(covered, total, overall_floor):
        failures.append(f"overall {overall:.2f}% is below {overall_floor:.2f}%")
    failures.extend(
        f"{row['project']} {row['line']:.2f}% is below {project_floor:.2f}%"
        for row in rows
        if below_threshold(int(row["covered"]), int(row["total"]), project_floor)
    )

    summary = {
        "scope": policy["scope"],
        "metric": policy["metric"],
        "overall": overall,
        "covered": covered,
        "total": total,
        "minimum_overall": overall_floor,
        "project_floor": project_floor,
        "projects": rows,
        "coverage_files": [str(path) for path in coverage_files],
    }
    output_root.mkdir(parents=True, exist_ok=True)
    (output_root / "coverage-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    with (output_root / "coverage-summary.txt").open("w", encoding="utf-8") as writer:
        writer.write(f"scope={summary['scope']}\nmetric={summary['metric']}\noverall={overall:.2f}\n")
        writer.write(f"minimum_overall={overall_floor:.2f}\nproject_floor={project_floor:.2f}\n")
        for row in rows:
            writer.write("project={project} covered={covered} total={total} line={line:.2f}\n".format(**row))

    print(f"coverage summary: {output_root / 'coverage-summary.txt'}")
    print(f"coverage summary json: {output_root / 'coverage-summary.json'}")
    for row in rows:
        print("coverage {project}: {line:.2f}% ({covered}/{total})".format(**row))
    print(f"coverage overall: {overall:.2f}% ({covered}/{total})")
    if failures:
        print("\n".join(f"ERROR: {failure}" for failure in failures), file=sys.stderr)
        return 1
    print("Coverage check OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
