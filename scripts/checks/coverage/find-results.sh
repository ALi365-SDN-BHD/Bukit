#!/usr/bin/env bash
set -euo pipefail

root="${1:?coverage root is required}"
expected="${2:-}"

files="$(find "$root" -type f -name 'coverage.cobertura.xml' | sort)"
count="$(printf '%s\n' "$files" | sed '/^$/d' | wc -l | tr -d ' ')"

if [[ "$count" -eq 0 ]]; then
  echo "ERROR: no coverage.cobertura.xml files found under ${root}" >&2
  exit 1
fi

if [[ -n "$expected" && "$count" -ne "$expected" ]]; then
  echo "ERROR: expected ${expected} coverage files under ${root}, found ${count}" >&2
  printf '%s\n' "$files" >&2
  exit 1
fi

printf '%s\n' "$files"
