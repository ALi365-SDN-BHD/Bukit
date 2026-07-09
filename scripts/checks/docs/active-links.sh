#!/usr/bin/env bash
set -euo pipefail

python3 - <<'PY'
import pathlib
import re
import sys

repo = pathlib.Path(".").resolve()
docs = []
for pattern in ("README*.md", "CONTRIBUTING*.md", "SECURITY*.md"):
    docs.extend(pathlib.Path(".").glob(pattern))
docs.append(pathlib.Path(".github/PULL_REQUEST_TEMPLATE.md"))
docs.extend(pathlib.Path("guide").rglob("*.md"))
docs.extend(pathlib.Path("docs").glob("compatibility-governance*.md"))
if pathlib.Path("docs/governance").is_dir():
    docs.extend(pathlib.Path("docs/governance").rglob("*.md"))

errors = []
for doc in sorted({path for path in docs if path.is_file()}):
    text = doc.read_text(encoding="utf-8")
    for match in re.finditer(r"\[[^\]]+\]\(([^)]+)\)", text):
        raw = match.group(1).strip()
        target = raw.split("#", 1)[0]
        if not target or re.match(r"^[a-z][a-z0-9+.-]*:", target):
            continue

        path = (doc.parent / target).resolve()
        try:
            path.relative_to(repo)
        except ValueError:
            errors.append(f"{doc}: link leaves repo: {raw}")
            continue

        if not path.exists():
            errors.append(f"{doc}: missing link target: {raw}")

if errors:
    print("active documentation link errors:", file=sys.stderr)
    print("\n".join(errors), file=sys.stderr)
    raise SystemExit(1)
PY

echo "active documentation links OK"
