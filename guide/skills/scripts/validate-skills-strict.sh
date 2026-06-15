#!/usr/bin/env bash
set -euo pipefail

SKILLS_DIR="$(cd "$(dirname "$0")/.." && pwd)"
REPO_ROOT="$(cd "$SKILLS_DIR/../.." && pwd)"
export SKILLS_DIR
export REPO_ROOT

python3 - "$SKILLS_DIR" "$REPO_ROOT" <<'PY'
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

import yaml

skills_dir = Path(sys.argv[1])
repo_root = Path(sys.argv[2])
errors: list[str] = []

index_path = skills_dir / "skills-index.yaml"
json_path = skills_dir / "skills-index.json"
plugin_path = skills_dir / "plugin.json"

index = yaml.safe_load(index_path.read_text(encoding="utf-8"))
plugin = json.loads(plugin_path.read_text(encoding="utf-8"))

skill_files = sorted(p for p in skills_dir.glob("*/SKILL.md") if p.parent.name not in {"scripts"})
skill_names = [p.parent.name for p in skill_files]
index_skills = index.get("skills", [])
index_names = [item["name"] for item in index_skills]

if index.get("skill_count") != len(skill_files):
    errors.append(f"skill_count {index.get('skill_count')} != actual {len(skill_files)}")
if len(index_skills) != len(skill_files):
    errors.append(f"index declares {len(index_skills)} skills != actual {len(skill_files)}")
if set(index_names) != set(skill_names):
    errors.append(f"index names differ from filesystem: index={index_names}, actual={skill_names}")

plugin_skills = plugin.get("skills", [])
expected_plugin_skills = [item["path"] for item in index_skills]
if plugin_skills != expected_plugin_skills:
    errors.append("plugin.json skills differ from skills-index.yaml paths")
if plugin.get("version") != index.get("version"):
    errors.append("plugin.json version differs from skills-index.yaml")
if any(path.startswith("labs/") for path in plugin_skills):
    errors.append("plugin.json must not include labs skills")
if (skills_dir / "labs").exists():
    errors.append("Labs skills must live under guide/labs-skills, not guide/skills/labs")

if json_path.exists():
    json_data = json.loads(json_path.read_text(encoding="utf-8"))
    if json.dumps(json_data, sort_keys=True, ensure_ascii=False) != json.dumps(index, sort_keys=True, ensure_ascii=False):
        errors.append("skills-index.json is out of sync with skills-index.yaml")
else:
    errors.append("skills-index.json is missing")

all_names = set(index_names)
for item in index_skills:
    for dep in item.get("requires", []):
        if dep not in all_names:
            errors.append(f"{item['name']} requires missing skill {dep}")
for workflow, value in index.get("workflows", {}).items():
    for skill in value.get("chain", []):
        if skill not in all_names:
            errors.append(f"workflow {workflow} references missing skill {skill}")

def read_front_matter(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        errors.append(f"{path.relative_to(skills_dir)} missing front matter")
        return {}
    end = text.find("\n---", 4)
    if end < 0:
        errors.append(f"{path.relative_to(skills_dir)} missing closing front matter")
        return {}
    try:
        return yaml.safe_load(text[4:end]) or {}
    except Exception as exc:
        errors.append(f"{path.relative_to(skills_dir)} invalid front matter: {exc}")
        return {}

required_fields = {"name", "description", "status", "since", "verified_by", "source_anchors", "guide_chapters"}
valid_statuses = {"stable", "beta", "experimental", "planned"}

for skill_path in skill_files:
    rel = skill_path.relative_to(skills_dir)
    meta = read_front_matter(skill_path)
    missing = required_fields - set(meta)
    if missing:
        errors.append(f"{rel} missing front matter fields: {', '.join(sorted(missing))}")
    if meta.get("name") != skill_path.parent.name:
        errors.append(f"{rel} name does not match directory")
    if meta.get("status") and meta["status"] not in valid_statuses:
        errors.append(f"{rel} invalid status {meta['status']}")
    if not str(meta.get("description", "")).startswith("Use when"):
        errors.append(f"{rel} description must start with 'Use when'")

    for field in ("verified_by", "source_anchors", "guide_chapters"):
        values = meta.get(field)
        if not isinstance(values, list) or not values:
            errors.append(f"{rel} {field} must be a non-empty list")
            continue
        for value in values:
            target = repo_root / value
            if not target.exists():
                errors.append(f"{rel} {field} path not found: {value}")

core_text_files = []
core_text_files.extend(skills_dir.glob("*.md"))
core_text_files.extend(skills_dir.glob("*.yaml"))
core_text_files.extend(skills_dir.glob("*.json"))
core_text_files.extend(skill_files)

for path in sorted(set(core_text_files)):
    rel = path.relative_to(skills_dir)
    text = path.read_text(encoding="utf-8")
    if "/Users/" in text or "/home/" in text or "file:///" in text:
        errors.append(f"{rel} contains local absolute path")
    if "src/skills/" in text:
        errors.append(f"{rel} contains old src/skills path")
    if "guide-0.1/" in text and rel.name != "README.md":
        errors.append(f"{rel} contains guide-0.1 path outside README context")
    legacy_dev_server_pattern = rf"\b" + "H" + "MR\b|Hot " + "Module Replacement"
    if re.search(legacy_dev_server_pattern, text):
        errors.append(f"{rel} contains forbidden dev-server terminology")

if errors:
    print("Strict skills metadata check failed:")
    for error in errors:
        print(f"  - {error}")
    sys.exit(1)

print("Strict skills metadata check passed")
PY

python3 "$SKILLS_DIR/scripts/check-cli-commands.py"

echo "All strict skill checks passed"
