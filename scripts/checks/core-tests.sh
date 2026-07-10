#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
projects_file="$(mktemp)"
trap 'rm -f "$projects_file"' EXIT

bash scripts/checks/coverage/list-core-projects.sh > "$projects_file"
bash scripts/checks/coverage/matrix.py "$projects_file" >/dev/null

while IFS=$'\t' read -r project filter; do
  [[ -n "$project" ]] || continue
  if [[ -n "$filter" ]]; then
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration" --filter "$filter"
  else
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration"
  fi
done < "$projects_file"
