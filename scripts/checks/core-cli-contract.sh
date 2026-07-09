#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

forbidden='bukit[[:space:]]+(docs|intent|theme|import|clone|visual|webhook|data|route)([[:space:]]|$)|--allow-external-plugins'
targets=(scripts .github)
matches="$(rg -n -i "$forbidden" "${targets[@]}" 2>/dev/null | rg -v '^scripts/checks/core-cli-contract.sh:' || true)"
if [ -n "$matches" ]; then
  echo "forbidden non-Core CLI usage found:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "Core CLI contract OK"
