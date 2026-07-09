#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
coverage_root="${BUKIT_COVERAGE_ROOT:-TestResults/coverage}"
projects_root="${coverage_root}/projects"
policy="${BUKIT_COVERAGE_POLICY:-docs/coverage-baselines.json}"
files_list="${coverage_root}/coverage-files.txt"
repo_root="$(pwd -P)"

if [[ -z "$coverage_root" || "$coverage_root" == "." || "$coverage_root" == ".." || "$coverage_root" == "/" || "$coverage_root" == "$repo_root" ]]; then
  echo "unsafe coverage output directory: ${coverage_root:-<empty>}" >&2
  exit 1
fi

run_step "coverage policy" bash scripts/checks/coverage-baseline-schema.sh "$policy"

rm -rf "$coverage_root"
mkdir -p "$projects_root"

expected_count=0
while IFS=$'\t' read -r project filter; do
  [[ -n "$project" ]] || continue
  expected_count=$((expected_count + 1))
  run_step "coverage project: $(basename "$(dirname "$project")")" \
    bash scripts/checks/coverage/run-one.sh "$configuration" "$project" "$projects_root" "$filter"
done < <(bash scripts/checks/coverage/list-core-projects.sh)

bash scripts/checks/coverage/find-results.sh "$projects_root" "$expected_count" > "$files_list"
run_step "coverage summary" bash scripts/checks/coverage/summarize.py "$policy" "$coverage_root" "$files_list"
