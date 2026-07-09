#!/usr/bin/env bash
set -euo pipefail

paths=(
  README.md
  README.zh-CN.md
  README.ms.md
  CONTRIBUTING.md
  CONTRIBUTING.zh-CN.md
  CONTRIBUTING.ms.md
  SECURITY.md
  SECURITY.zh-CN.md
  SECURITY.ms.md
  .github/PULL_REQUEST_TEMPLATE.md
  .github/workflows/ci.yaml
  .github/workflows/release.yaml
  docs/compatibility-governance.md
  docs/compatibility-governance.zh-CN.md
)

while IFS= read -r path; do
  paths+=("$path")
done < <(find guide -type f -name '*.md' | sort)

if [ -d docs/governance ]; then
  while IFS= read -r path; do
    paths+=("$path")
  done < <(find docs/governance -type f -name '*.md' | sort)
fi

matches="$(rg -n '(/Users/|file:///Users/)' "${paths[@]}" || true)"
if [ -n "$matches" ]; then
  echo "local absolute paths found in public documentation surfaces:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "public absolute path scan OK"
