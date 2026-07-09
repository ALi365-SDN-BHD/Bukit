#!/usr/bin/env bash
set -euo pipefail

doc_limit=1000
script_limit=200
failures=()

check_limit() {
  local path="$1"
  local limit="$2"
  local lines
  lines="$(wc -l < "$path" | tr -d ' ')"
  if [ "$lines" -gt "$limit" ]; then
    failures+=("$path has $lines lines; limit is $limit")
  fi
}

while IFS= read -r path; do
  check_limit "$path" "$doc_limit"
done < <(
  {
    find . -maxdepth 1 -type f \( -name 'README*.md' -o -name 'CONTRIBUTING*.md' -o -name 'SECURITY*.md' \)
    find .github -maxdepth 1 -type f -name '*.md'
    find guide -type f -name '*.md'
    find docs -maxdepth 1 -type f -name 'compatibility-governance*.md'
    [ ! -d docs/governance ] || find docs/governance -type f -name '*.md'
  } | sort
)

while IFS= read -r path; do
  check_limit "$path" "$script_limit"
done < <(find scripts guide/skills/scripts -type f -name '*.sh' | sort)

if [ "${#failures[@]}" -gt 0 ]; then
  echo "active documentation/script size policy violations:" >&2
  printf '%s\n' "${failures[@]}" >&2
  exit 1
fi

echo "active size policy OK"
