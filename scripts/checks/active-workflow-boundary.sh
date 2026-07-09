#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

targets=(scripts)
if [[ -d .github/workflows ]]; then
  targets=(.github/workflows scripts)
fi

forbidden=(
  ".github/workflows-0.1"
  "workflows-0.1"
  "scripts-0.1"
  "scripts-0.2"
  "guide-0.1"
  "guide-0.2"
)

failed=0
for pattern in "${forbidden[@]}"; do
  matches="$(grep -RIn --exclude='active-workflow-boundary.sh' -- "$pattern" "${targets[@]}" || true)"
  if [[ -n "$matches" ]]; then
    printf 'backup/reference path referenced from active workflow/script surface: %s\n' "$pattern" >&2
    printf '%s\n' "$matches" >&2
    failed=1
  fi
done

if [[ "$failed" -ne 0 ]]; then
  exit 1
fi

echo "Active workflow boundary OK"
