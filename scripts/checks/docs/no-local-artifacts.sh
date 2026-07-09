#!/usr/bin/env bash
set -euo pipefail

matches="$(
  find . \
    -path './.git' -prune -o \
    -path './.worktrees' -prune -o \
    -type d \( -name 'guide-0*' -o -name 'scripts-0*' \) -prune -o \
    -type d \( -name bin -o -name obj \) -prune -o \
    -type f \( \
      -name '.DS_Store' -o \
      -name '.env' -o \
      -name '.env.*' -o \
      -name '*.db' -o \
      -name '*.sqlite' -o \
      -name '*.coverage' -o \
      -name '*.trx' -o \
      -name '*.binlog' -o \
      -name '*.tmp' -o \
      -name '*.bak' -o \
      -name 'openapi.json' -o \
      -name 'coverage.cobertura.xml' \
    \) -print | sort
)"

matches="$(
  printf '%s\n' "$matches" |
    sed '/^$/d' |
    sed '\#^\./tests/fixtures/dotfile-leak-site/static/\.env$#d' |
    sed '\#^\./\.env\.example$#d'
)"
if [ -n "$matches" ]; then
  echo "local artifacts found outside the allowlist:" >&2
  echo "$matches" >&2
  exit 1
fi

echo "recursive local artifact scan OK"
