#!/usr/bin/env python3
from __future__ import annotations

import re
from pathlib import Path

root = Path("guide/skills")
index = root / "skills-index.yaml"
if not index.exists():
    raise SystemExit("guide/skills/skills-index.yaml is missing")

errors: list[str] = []
skill_files = sorted(root.glob("*/SKILL.md"))
if not skill_files:
    errors.append("guide/skills has no skill directories")

index_text = index.read_text(encoding="utf-8")
for path in skill_files:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        errors.append(f"{path}: missing frontmatter")
        continue
    end = text.find("\n---", 4)
    if end < 0:
        errors.append(f"{path}: unclosed frontmatter")
        continue
    frontmatter = text[4:end]
    name_match = re.search(r"^name:\s*([A-Za-z0-9_-]+)", frontmatter, re.MULTILINE)
    desc_match = re.search(r"^description:\s*.+", frontmatter, re.MULTILINE)
    if not name_match:
        errors.append(f"{path}: missing name")
    elif name_match.group(1) not in index_text:
        errors.append(f"{path}: skill name not listed in skills-index.yaml")
    if not desc_match:
        errors.append(f"{path}: missing description")

if errors:
    for error in errors:
        print(error)
    raise SystemExit(1)

print("skills schema OK")
