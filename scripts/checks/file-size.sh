#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

max_file_lines="${MAX_FILE_LINES:-600}"
oversized_baseline="${OVERSIZED_BASELINE:-scripts/.oversized-baseline.txt}"

current_oversized="$(find src -type f -name '*.cs' \
  -not -path '*/obj/*' \
  -not -path '*/bin/*' \
  -not -path '*/.codex-tmp*/*' \
  -exec wc -l {} + 2>/dev/null \
  | awk -v limit="$max_file_lines" '$1 > limit && $2 != "total" { print $2 }' \
  | sort -u)"

baseline_paths=""
if [ -f "$oversized_baseline" ]; then
  baseline_paths="$(grep -vE '^\s*(#|$)' "$oversized_baseline" | sort -u || true)"
fi

new_oversized="$(comm -23 <(printf '%s\n' "$current_oversized") <(printf '%s\n' "$baseline_paths"))"

if [ -n "$baseline_paths" ] && [ -n "$current_oversized" ]; then
  grandfathered_still_present="$(comm -12 <(printf '%s\n' "$current_oversized") <(printf '%s\n' "$baseline_paths"))"
  if [ -n "$grandfathered_still_present" ]; then
    echo "WARNING: pre-existing oversized files:"
    while IFS= read -r path; do
      [ -z "$path" ] && continue
      lines="$(wc -l <"$path" 2>/dev/null | tr -d ' ')"
      echo "  ${lines:-?} lines  $path"
    done <<<"$grandfathered_still_present"
  fi
fi

if [ -n "$new_oversized" ]; then
  echo "ERROR: the following .cs files exceed ${max_file_lines} lines and are not in ${oversized_baseline}:" >&2
  while IFS= read -r path; do
    [ -z "$path" ] && continue
    lines="$(wc -l <"$path" 2>/dev/null | tr -d ' ')"
    echo "  ${lines:-?} lines  $path" >&2
  done <<<"$new_oversized"
  exit 1
fi

echo "File-size check OK"
