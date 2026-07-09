#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"

while IFS=$'\t' read -r project filter; do
  [[ -n "$project" ]] || continue
  if [[ -n "$filter" ]]; then
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration" --filter "$filter"
  else
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration"
  fi
done < <(bash scripts/checks/coverage/list-core-projects.sh)
