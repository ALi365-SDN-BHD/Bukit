#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

python3 - <<'PY'
from __future__ import annotations

import json
import sys
from pathlib import Path

import yaml

schemas = {
    "docs/schemas/skills-index.v1.json": {
        "required": [
            "version",
            "updated",
            "scope",
            "skill_count",
            "core_commands",
            "labs_not_core",
            "skills",
            "workflows",
        ],
        "skill_required": [
            "name",
            "status",
            "since",
            "type",
            "priority",
            "path",
            "requires",
            "source_anchors",
            "verified_by",
            "guide_chapters",
            "description",
        ],
    },
    "docs/schemas/skill-frontmatter.v1.json": {
        "required": [
            "name",
            "description",
            "status",
            "since",
            "verified_by",
            "source_anchors",
            "guide_chapters",
        ],
    },
}

errors: list[str] = []
schema_cache: dict[str, dict] = {}

for rel, expectations in schemas.items():
    path = Path(rel)
    if not path.exists():
        errors.append(f"missing schema: {rel}")
        continue

    try:
        schema = json.loads(path.read_text(encoding="utf-8"))
        schema_cache[rel] = schema
    except json.JSONDecodeError as exc:
        errors.append(f"{rel} invalid JSON: {exc}")
        continue

    if schema.get("$schema") != "https://json-schema.org/draft/2020-12/schema":
        errors.append(f"{rel} must use draft 2020-12")

    missing = sorted(set(expectations["required"]) - set(schema.get("required", [])))
    if missing:
        errors.append(f"{rel} missing required keys: {', '.join(missing)}")

    if "skill_required" in expectations:
        skill = schema.get("$defs", {}).get("skill", {})
        missing = sorted(set(expectations["skill_required"]) - set(skill.get("required", [])))
        if missing:
            errors.append(f"{rel} skill definition missing required keys: {', '.join(missing)}")

index_path = Path("guide/skills/skills-index.yaml")
if index_path.exists() and "docs/schemas/skills-index.v1.json" in schema_cache:
    index_schema = schema_cache["docs/schemas/skills-index.v1.json"]
    index = yaml.safe_load(index_path.read_text(encoding="utf-8")) or {}
    missing = sorted(set(index_schema.get("required", [])) - set(index))
    if missing:
        errors.append(f"{index_path} missing schema-required keys: {', '.join(missing)}")

    skill_schema = index_schema.get("$defs", {}).get("skill", {})
    skill_required = set(skill_schema.get("required", []))
    for offset, skill in enumerate(index.get("skills", []), start=1):
        missing = sorted(skill_required - set(skill))
        if missing:
            errors.append(f"{index_path} skills[{offset}] missing keys: {', '.join(missing)}")
else:
    errors.append(f"{index_path} is missing")

frontmatter_schema = schema_cache.get("docs/schemas/skill-frontmatter.v1.json", {})
frontmatter_required = set(frontmatter_schema.get("required", []))
for skill_path in sorted(Path("guide/skills").glob("*/SKILL.md")):
    text = skill_path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        errors.append(f"{skill_path} missing front matter")
        continue

    end = text.find("\n---", 4)
    if end < 0:
        errors.append(f"{skill_path} missing closing front matter")
        continue

    frontmatter = yaml.safe_load(text[4:end]) or {}
    missing = sorted(frontmatter_required - set(frontmatter))
    if missing:
        errors.append(f"{skill_path} missing frontmatter schema keys: {', '.join(missing)}")

if errors:
    print("Skills schema check failed:")
    for error in errors:
        print(f"  - {error}")
    sys.exit(1)

print("Skills schema files OK")
PY

bash guide/skills/scripts/validate-skills-strict.sh
