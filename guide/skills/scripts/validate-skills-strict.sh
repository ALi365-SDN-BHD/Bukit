#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../../.."

bash scripts/checks/skills-schema.sh
python3 guide/skills/scripts/check-cli-commands.py

forbidden='bukit[[:space:]]+(init|create|clone|import|intent|webhook|theme|notion|visual|data|route)([[:space:]]|$)'
matches="$(rg -n "$forbidden" guide/skills 2>/dev/null || true)"
if [ -n "$matches" ]; then
  echo "non-Core command examples found in Core skills:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "skills strict validation OK"
