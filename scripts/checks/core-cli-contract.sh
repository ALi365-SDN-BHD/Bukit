#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

forbidden='bukit[[:space:]]+(docs|intent|theme|import|clone|visual|webhook|data|route)([[:space:]]|$)|--allow-external-plugins'
targets=(scripts .github)
grep_status=0
matches="$(grep -RIniE --exclude='core-cli-contract.sh' -- "$forbidden" "${targets[@]}")" || grep_status=$?
if ((grep_status > 1)); then
  echo "Core CLI contract text search failed" >&2
  exit "$grep_status"
fi

if [ -n "$matches" ]; then
  echo "forbidden non-Core CLI usage found:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "Core CLI contract OK"
