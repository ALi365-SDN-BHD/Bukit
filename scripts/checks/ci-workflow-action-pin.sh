#!/usr/bin/env bash

set -euo pipefail

workflows_dir="${1:-.github/workflows}"

command -v rg >/dev/null 2>&1 || {
  echo "ERROR: rg is required for regex-aware workflow scanning." >&2
  exit 2
}

search_cmd=(rg -n --no-heading --pcre2 'uses:' "$workflows_dir" --glob '*.yml' --glob '*.yaml')

if [ ! -d "$workflows_dir" ]; then
  echo "workflow directory not found: $workflows_dir" >&2
  exit 2
fi

echo "== checks: github action pins in ${workflows_dir} =="

has_violations=0

while IFS= read -r entry; do
  file="${entry%%:*}"
  rest="${entry#*:}"
  line="${rest%%:*}"
  uses_line="${entry#*:[0-9]*:}"
  uses_ref="${uses_line##*uses: }"
  uses_ref="$(printf '%s' "$uses_ref" | sed -E 's/[[:space:]]+#.*$//; s/^[[:space:]]+//; s/[[:space:]]+$//')"

  if [ -z "$uses_ref" ]; then
    continue
  fi

  if [[ "$uses_ref" == ./* ]] || [[ "$uses_ref" == docker://* ]] || [[ "$uses_ref" == github.com/* ]]; then
    continue
  fi

  if [[ "$uses_ref" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(/[A-Za-z0-9_.-]+)*@[a-f0-9]{40}$ ]]; then
    continue
  fi

  has_violations=1
  echo "${file}:${line}: unpinned action reference -> ${uses_ref}" >&2
done < <("${search_cmd[@]}")

if [ "$has_violations" -eq 1 ]; then
  echo "GitHub Action pin compliance check failed: found unpinned action references (must use full commit SHA)." >&2
  exit 1
fi

echo "GitHub Action pin compliance check passed."
