#!/usr/bin/env bash
set -euo pipefail

required=(
  CONTRIBUTING.md
  CONTRIBUTING.zh-CN.md
  CONTRIBUTING.ms.md
  SECURITY.md
  SECURITY.zh-CN.md
  SECURITY.ms.md
  .github/PULL_REQUEST_TEMPLATE.md
  .github/workflows/ci.yaml
  .github/workflows/release.yaml
  scripts/quality-gate.sh
  scripts/gates/ci-fast.sh
  scripts/gates/ci-full.sh
  scripts/security/security-regression.sh
  guide/dev/release.md
  guide/dev/testing.md
  guide/dev/documentation-governance.md
  guide/dev/plugins.md
  guide/dev/config-site-yaml.md
  guide/dev/publish-deploy.md
  guide/labs/webhook.md
)

for path in "${required[@]}"; do
  [ -e "$path" ] || {
    echo "missing public documentation contract path: $path" >&2
    exit 1
  }
done

docs=(
  CONTRIBUTING.md
  CONTRIBUTING.zh-CN.md
  CONTRIBUTING.ms.md
  SECURITY.md
  SECURITY.zh-CN.md
  SECURITY.ms.md
  .github/PULL_REQUEST_TEMPLATE.md
)

grep_status=0
forbidden_matches="$(grep -nE -- \
  '(\.github/workflows/ci\.yml|scripts/smoke\.ps1|scripts/check-aot-warnings\.sh|scripts/check-doc-asset-consistency\.ps1|guide/dev/new-developer-30min\.md|guide/dev/code-wiki\.md|guide/dev/governance-checklist\.md|guide/dev/testing-smoke\.md|guide/dev/webhook\.md|src/Bukit\.Core|bukit webhook|BUKIT_NOTION_TOKEN|coverage-report/Summary\.txt|build \+ test \+ coverage \+ format \+ smoke|quality-gate 自动检查|quality-gate 自動檢查|WASM)' \
  "${docs[@]}")" || grep_status=$?

if ((grep_status > 1)); then
  echo "public documentation text search failed" >&2
  exit "$grep_status"
fi

if [[ -n "$forbidden_matches" ]]; then
  echo "stale public documentation references found:" >&2
  echo "$forbidden_matches" >&2
  exit 1
fi

python3 - <<'PY'
import pathlib
import re
import sys

docs = [
    pathlib.Path("CONTRIBUTING.md"),
    pathlib.Path("CONTRIBUTING.zh-CN.md"),
    pathlib.Path("CONTRIBUTING.ms.md"),
    pathlib.Path("SECURITY.md"),
    pathlib.Path("SECURITY.zh-CN.md"),
    pathlib.Path("SECURITY.ms.md"),
    pathlib.Path(".github/PULL_REQUEST_TEMPLATE.md"),
]
errors = []

for doc in docs:
    text = doc.read_text(encoding="utf-8")
    for match in re.finditer(r"\[[^\]]+\]\(([^)]+)\)", text):
        target = match.group(1).split("#", 1)[0]
        if not target or re.match(r"^[a-z][a-z0-9+.-]*:", target):
            continue
        path = (doc.parent / target).resolve()
        if not path.exists():
            errors.append(f"{doc}:{match.start(1)} missing link target: {target}")
    for match in re.finditer(r"`([^`]+)`", text):
        for target in re.findall(r"(?:scripts|guide|\.github)/[A-Za-z0-9._/-]+", match.group(1)):
            path = pathlib.Path(target.rstrip(".,:;"))
            if not path.exists():
                errors.append(f"{doc}:{match.start(1)} missing code path: {target}")

if errors:
    print("public documentation link errors:", file=sys.stderr)
    print("\n".join(errors), file=sys.stderr)
    raise SystemExit(1)
PY

echo "public documentation contracts OK"
