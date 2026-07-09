#!/usr/bin/env python3
from __future__ import annotations

import json
import re
from pathlib import Path

src = Path("src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs").read_text(encoding="utf-8")

commands: list[str] = []
for match in re.finditer(r"var\s+(\w+)\s*=\s*new\s+CliCommandSpec\(\s*Name:\s*\"([^\"]+)\"", src):
    commands.append(match.group(2))

subcommands: list[str] = []
for parent in ("config", "seo", "geo", "publish"):
    start = src.find(f'Name: "{parent}"')
    if start < 0:
        continue
    end = src.find("\n        var ", start + 1)
    if end < 0:
        end = src.find("\n        return ", start + 1)
    block = src[start:end]
    for sub in re.findall(r"new\s+CliCommandSpec\(\s*Name:\s*\"([^\"]+)\"", block):
        if sub != parent:
            subcommands.append(f"{parent} {sub}")

options = sorted(set(re.findall(r"new\s+CliOptionSpec\(\"(--[a-z0-9-]+)\"", src)))
arguments = sorted(set(re.findall(r"new\s+CliArgumentSpec\(\"([a-z][a-z0-9-]*)\"", src)))

print(json.dumps({
    "commands": commands,
    "subcommands": subcommands,
    "options": options,
    "arguments": arguments,
}, indent=2, sort_keys=True))
