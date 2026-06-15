#!/usr/bin/env python3
"""Validate Bukit Core CLI command references in guide/skills.

The source of truth is src/Bukit.Cli/Cli/BukitCliSpecs.cs.
The primary documented surface is guide/skills/bukit-cli-reference/SKILL.md.
All top-level Core skill files are also scanned for accidental non-Core command
references. Labs and validator scripts are intentionally skipped.
"""

from __future__ import annotations

import os
import re
import sys
from pathlib import Path


def repo_root() -> Path:
    return Path(os.environ.get("REPO_ROOT", Path.cwd())).resolve()


def skills_dir() -> Path:
    env = os.environ.get("SKILLS_DIR")
    if env:
        return Path(env).resolve()
    return (repo_root() / "guide" / "skills").resolve()


def find_matching_paren(text: str, open_index: int) -> int:
    depth = 0
    in_string = False
    escape = False
    for index in range(open_index, len(text)):
        ch = text[index]
        if in_string:
            if escape:
                escape = False
            elif ch == "\\":
                escape = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
            continue
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return index
    raise ValueError("unmatched parenthesis in BukitCliSpecs.cs")


def extract_options(block: str, diff_options: list[str]) -> list[str]:
    if "Options: DiffOptions()" in block:
        return diff_options
    options = re.findall(r'new\s+CliOptionSpec\(\s*"(--[a-z0-9-]+)"', block, re.IGNORECASE)
    return sorted(set(options))


def extract_arguments(block: str) -> list[str]:
    arguments = re.findall(r'new\s+CliArgumentSpec\(\s*"([a-z0-9-]+)"', block, re.IGNORECASE)
    return sorted({f"<{argument}>" for argument in arguments})


def extract_parameters(block: str, diff_options: list[str]) -> list[str]:
    return sorted(set(extract_options(block, diff_options)) | set(extract_arguments(block)))


def extract_spec() -> tuple[dict[str, list[str]], set[str]]:
    spec_path = repo_root() / "src" / "Bukit.Cli" / "Cli" / "BukitCliSpecs.cs"
    if not spec_path.exists():
        raise FileNotFoundError(f"Missing CLI spec: {spec_path}")

    text = spec_path.read_text(encoding="utf-8")
    diff_block = text[text.find("private static CliOptionSpec[] DiffOptions()"):]
    diff_options = sorted(set(re.findall(r'new\s+CliOptionSpec\("--[a-z0-9-]+"', diff_block, re.IGNORECASE)))
    diff_options = [item.split('"')[1] for item in diff_options]

    commands: dict[str, list[str]] = {}
    parents_with_subcommands: set[str] = set()

    for match in re.finditer(r"\bvar\s+\w+\s*=\s*new\s+CliCommandSpec\s*\(", text):
        open_index = text.find("(", match.start())
        close_index = find_matching_paren(text, open_index)
        block = text[match.start(): close_index + 1]

        name_match = re.search(r'Name:\s*"([^"]+)"', block)
        if not name_match:
            continue
        parent = name_match.group(1)

        parent_part = block.split("Subcommands:", 1)[0]
        commands[parent] = extract_parameters(parent_part, diff_options)

        if "Subcommands:" not in block:
            continue

        parents_with_subcommands.add(parent)
        sub_part = block.split("Subcommands:", 1)[1]
        for sub_match in re.finditer(r"new\s+CliCommandSpec\s*\(", sub_part):
            sub_open = sub_part.find("(", sub_match.start())
            sub_close = find_matching_paren(sub_part, sub_open)
            sub_block = sub_part[sub_match.start(): sub_close + 1]
            sub_name_match = re.search(r'Name:\s*"([^"]+)"', sub_block)
            if not sub_name_match:
                continue
            command_path = f"{parent} {sub_name_match.group(1)}"
            commands[command_path] = extract_parameters(sub_block, diff_options)

    return commands, parents_with_subcommands


def extract_reference() -> dict[str, list[str]]:
    ref_path = skills_dir() / "bukit-cli-reference" / "SKILL.md"
    if not ref_path.exists():
        raise FileNotFoundError(f"Missing CLI reference skill: {ref_path}")

    commands: dict[str, list[str]] = {}
    in_table = False
    for line in ref_path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped == "| Command | Purpose | Key Parameters |":
            in_table = True
            continue
        if in_table and (not stripped.startswith("|") or stripped.startswith("|---")):
            if not stripped.startswith("|---"):
                in_table = False
            continue
        if not in_table:
            continue

        match = re.match(r"\| `([^`]+)` \| [^|]+ \| (.+) \|", stripped)
        if not match:
            continue
        command = match.group(1).strip()
        params = sorted(set(re.findall(r"`(--[a-z0-9-]+|<[^`]+>)`", match.group(2), re.IGNORECASE)))
        commands[command] = params
    return commands


def scan_core_command_mentions(allowed: set[str], parents_with_subcommands: set[str]) -> list[str]:
    errors: list[str] = []
    root = skills_dir()
    candidates: list[Path] = []
    candidates.extend(root.glob("*.md"))
    candidates.extend(root.glob("*.yaml"))
    candidates.extend(root.glob("*.json"))
    candidates.extend(root.glob("*/SKILL.md"))

    for path in sorted(set(candidates)):
        rel = path.relative_to(root)
        if rel.parts[0] in {"scripts"}:
            continue
        text = path.read_text(encoding="utf-8")
        fragments: list[str] = []
        in_fence = False
        fence_info = ""
        fence_lines: list[str] = []
        for line in text.splitlines():
            if line.strip().startswith("```"):
                if in_fence:
                    if fence_info in {"bash", "sh", "shell", "console", "zsh", "powershell", "ps1"}:
                        fragments.append("\n".join(fence_lines))
                    fence_lines = []
                    fence_info = ""
                    in_fence = False
                else:
                    in_fence = True
                    fence_info = line.strip().removeprefix("```").strip().lower()
                continue
            if in_fence:
                fence_lines.append(line)

        text_without_fences = re.sub(r"```.*?```", "", text, flags=re.DOTALL)
        fragments.extend(re.findall(r"(?<!`)`([^`\n]*\bbukit\b[^`\n]*)`(?!`)", text_without_fences, re.IGNORECASE))

        for fragment in fragments:
            for match in re.finditer(r"(?:^|[\s$>])bukit\s+([a-z][a-z0-9-]*)(?:\s+([a-z][a-z0-9-]*))?", fragment, re.IGNORECASE | re.MULTILINE):
                first = match.group(1).lower()
                second = (match.group(2) or "").lower()
                if first not in allowed:
                    errors.append(f"{rel}: references non-Core command family 'bukit {first}'")
                    continue
                if second and first in parents_with_subcommands:
                    command_path = f"{first} {second}"
                    if command_path not in allowed:
                        errors.append(f"{rel}: references unsupported subcommand 'bukit {command_path}'")
    return errors


def main() -> int:
    spec_commands, parents_with_subcommands = extract_spec()
    ref_commands = extract_reference()

    errors: list[str] = []
    spec_set = set(spec_commands)
    ref_set = set(ref_commands)

    missing = sorted(spec_set - ref_set)
    extra = sorted(ref_set - spec_set)
    if missing:
        errors.append("Commands in source but missing from CLI reference: " + ", ".join(missing))
    if extra:
        errors.append("Commands in CLI reference but not in source: " + ", ".join(extra))

    for command in sorted(spec_set & ref_set):
        spec_opts = set(spec_commands[command])
        ref_opts = set(ref_commands[command])
        missing_opts = sorted(spec_opts - ref_opts)
        extra_opts = sorted(ref_opts - spec_opts)
        if missing_opts:
            errors.append(f"{command}: source options missing from reference: {', '.join(missing_opts)}")
        if extra_opts:
            errors.append(f"{command}: reference options not in source: {', '.join(extra_opts)}")

    errors.extend(scan_core_command_mentions(spec_set, parents_with_subcommands))

    if errors:
        print("CLI consistency check failed:")
        for error in errors:
            print(f"  - {error}")
        return 1

    print(f"CLI consistency check passed: {len(spec_set)} command paths")
    return 0


if __name__ == "__main__":
    sys.exit(main())
