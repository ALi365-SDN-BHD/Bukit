#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

python3 guide/skills/scripts/check-cli-commands.py

python3 - <<'PY'
from __future__ import annotations

import re
import sys
import importlib.util
from pathlib import Path

repo = Path.cwd()
checker_path = repo / "guide" / "skills" / "scripts" / "check-cli-commands.py"
spec = importlib.util.spec_from_file_location("check_cli_commands", checker_path)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load CLI checker: {checker_path}")
check_cli_commands = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check_cli_commands)

spec_commands, parents_with_subcommands = check_cli_commands.extract_spec()
all_commands = set(spec_commands)
top_level_commands = {command for command in all_commands if " " not in command}

errors: list[str] = []


def table_commands(path: Path) -> set[str]:
    commands: set[str] = set()
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        match = re.match(r"\| `([a-z][a-z0-9-]*(?: [a-z][a-z0-9-]*)?)` \|", line)
        if match and match.group(1).strip() in all_commands:
            commands.add(match.group(1).strip())
    return commands


def expected_params(command: str) -> set[str]:
    return set(spec_commands[command])


def table_command_params(path: Path) -> dict[str, set[str]]:
    commands: dict[str, set[str]] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line.startswith("| `"):
            continue

        cells = [cell.strip() for cell in line.strip("|").split("|")]
        if len(cells) < 3:
            continue

        command = cells[0].strip("`")
        if command not in all_commands:
            continue

        params_cell = cells[2]
        if params_cell.lower() == "none":
            commands[command] = set()
            continue

        if params_cell.startswith("same diff options"):
            commands[command] = set(spec_commands["seo diff"])
            continue

        params = set(re.findall(r"`(--[a-z0-9-]+|<[^`]+>)`", params_cell, re.IGNORECASE))
        commands[command] = params
    return commands


def compare_table(path_name: str, expected: set[str]) -> None:
    path = repo / path_name
    if not path.exists():
        errors.append(f"{path_name}: missing file")
        return
    actual = table_commands(path)
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    if missing:
        errors.append(f"{path_name}: missing commands: {', '.join(missing)}")
    if extra:
        errors.append(f"{path_name}: extra commands: {', '.join(extra)}")


def compare_params_table(path_name: str) -> None:
    path = repo / path_name
    if not path.exists():
        errors.append(f"{path_name}: missing file")
        return

    actual = table_command_params(path)
    if not actual:
        errors.append(f"{path_name}: missing command option/argument table")
        return

    missing_commands = sorted(all_commands - set(actual))
    if missing_commands:
        errors.append(f"{path_name}: missing option rows: {', '.join(missing_commands)}")

    for command in sorted(all_commands & set(actual)):
        expected = expected_params(command)
        documented = actual[command]
        missing = sorted(expected - documented)
        extra = sorted(documented - expected)
        if missing:
            errors.append(f"{path_name}: {command}: missing parameters: {', '.join(missing)}")
        if extra:
            errors.append(f"{path_name}: {command}: extra parameters: {', '.join(extra)}")


def reject_unknown_options(path_name: str) -> None:
    path = repo / path_name
    if not path.exists():
        return

    allowed_options = {option for options in spec_commands.values() for option in options}
    text = path.read_text(encoding="utf-8")
    for line_no, line in enumerate(text.splitlines(), start=1):
        for option in re.findall(r"`(--[a-z0-9-]+)`", line, re.IGNORECASE):
            if option not in allowed_options:
                errors.append(f"{path_name}:{line_no}: unknown option '{option}'")


def command_mentions(path_name: str) -> None:
    path = repo / path_name
    if not path.exists():
        return
    text = path.read_text(encoding="utf-8")
    fragments: list[tuple[int, str]] = []
    for line_no, line in enumerate(text.splitlines(), start=1):
        for match in re.finditer(r"`([^`\n]*\bbukit\b[^`\n]*)`", line, re.IGNORECASE):
            fragments.append((line_no, match.group(1)))

    fence = False
    fence_lines: list[tuple[int, str]] = []
    for line_no, line in enumerate(text.splitlines(), start=1):
        if line.strip().startswith("```"):
            if fence:
                fragments.extend(fence_lines)
                fence_lines = []
                fence = False
            else:
                fence = True
            continue
        if fence:
            fence_lines.append((line_no, line))

    for line_no, fragment in fragments:
        for match in re.finditer(r"(?:^|[\s$>])bukit\s+([a-z][a-z0-9-]*)(?:\s+([a-z][a-z0-9-]*))?", fragment, re.IGNORECASE):
            first = match.group(1).lower()
            second = (match.group(2) or "").lower()
            if first not in top_level_commands:
                errors.append(f"{path_name}:{line_no}: unsupported command family 'bukit {first}'")
                continue
            if second and first in parents_with_subcommands:
                command = f"{first} {second}"
                if command not in all_commands:
                    errors.append(f"{path_name}:{line_no}: unsupported subcommand 'bukit {command}'")


for readme in ("README.md", "README.zh-CN.md", "README.ms.md"):
    compare_table(readme, top_level_commands)

for guide in ("guide/user/12-cli-reference.md", "guide/dev/cli.md", "guide/skills/bukit-cli-reference/SKILL.md"):
    compare_table(guide, all_commands)

for guide in ("guide/dev/cli.md", "guide/skills/bukit-cli-reference/SKILL.md"):
    compare_params_table(guide)

for path in (
    "README.md",
    "README.zh-CN.md",
    "README.ms.md",
    "guide/user/12-cli-reference.md",
    "guide/dev/cli.md",
    "guide/skills/bukit-cli-reference/SKILL.md",
):
    reject_unknown_options(path)

for path in (
    "README.md",
    "README.zh-CN.md",
    "README.ms.md",
    "guide/user/12-cli-reference.md",
    "guide/dev/cli.md",
    "guide/dev/release.md",
    "guide/dev/release-checklist.md",
):
    command_mentions(path)

if errors:
    print("CLI docs sync check failed:")
    for error in errors:
        print(f"  - {error}")
    raise SystemExit(1)

print(
    "CLI docs sync check OK: "
    f"{len(top_level_commands)} top-level commands, {len(all_commands)} command paths"
)
PY
