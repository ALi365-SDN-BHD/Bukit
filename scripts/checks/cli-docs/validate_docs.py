#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

payload = json.loads(subprocess.check_output([
    sys.executable,
    "scripts/checks/cli-docs/extract_cli.py",
], text=True))

docs = [
    Path("README.md"),
    Path("README.zh-CN.md"),
    Path("README.ms.md"),
    Path("guide/user/12-cli-reference.md"),
    Path("guide/dev/cli.md"),
    Path("guide/skills/bukit-cli-reference/SKILL.md"),
]

errors: list[str] = []
for doc in docs:
    if not doc.exists():
        errors.append(f"{doc}: missing")
        continue
    text = doc.read_text(encoding="utf-8")
    for command in payload["commands"]:
        if f"`{command}`" not in text:
            errors.append(f"{doc}: missing command `{command}`")

for doc in [Path("guide/user/12-cli-reference.md"), Path("guide/dev/cli.md"), Path("guide/skills/bukit-cli-reference/SKILL.md")]:
    if not doc.exists():
        continue
    text = doc.read_text(encoding="utf-8")
    for command in payload["subcommands"]:
        if f"`{command}`" not in text:
            errors.append(f"{doc}: missing subcommand `{command}`")
    for option in payload["options"]:
        if f"`{option}`" not in text:
            errors.append(f"{doc}: missing option `{option}`")

allowed = set(payload["commands"])
for doc in docs:
    if not doc.exists():
        continue
    for line_no, line in enumerate(doc.read_text(encoding="utf-8").splitlines(), 1):
        stripped = line.strip()
        if not (stripped.startswith("bukit ") or "`bukit " in line or "$ bukit " in line):
            continue
        for match in re.finditer(r"\bbukit\s+([a-z][a-z0-9-]*)", line):
            command = match.group(1)
            if command not in allowed and command != "help":
                errors.append(f"{doc}:{line_no}: unsupported Core command `bukit {command}`")

if errors:
    print("CLI docs sync failed:", file=sys.stderr)
    for error in errors:
        print(f"  - {error}", file=sys.stderr)
    raise SystemExit(1)

print("CLI docs sync OK")
