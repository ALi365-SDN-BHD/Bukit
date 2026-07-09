#!/usr/bin/env bash
set -euo pipefail

matches="$(find guide scripts -name '.DS_Store' -o -name '*.tmp' -o -name '*.bak' 2>/dev/null || true)"
if [ -n "$matches" ]; then
  echo "local artifacts found:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "guide/scripts local artifact scan OK"
