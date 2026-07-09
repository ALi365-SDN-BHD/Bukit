#!/usr/bin/env bash
set -euo pipefail

scan_targets=(README.md README.zh-CN.md README.ms.md guide/user guide/dev guide/skills)
forbidden='bukit[[:space:]]+(init|create|clone|import|intent|webhook|theme|notion|visual|data|route)([[:space:]]|$)'

matches="$(rg -n "$forbidden" "${scan_targets[@]}" 2>/dev/null || true)"
if [ -n "$matches" ]; then
  echo "non-Core command examples found in Core docs:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "Core docs command boundary OK"
