#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
coverage_root="${BUKIT_COVERAGE_ROOT:-TestResults/coverage}"
policy="${BUKIT_COVERAGE_POLICY:-docs/coverage-baselines.json}"
repo_root="$(pwd -P)"
coverage_root="$(bash scripts/checks/coverage/validate-output-root.py "$coverage_root" "$repo_root")"
projects_root="${coverage_root}/projects"
files_list="${coverage_root}/coverage-files.txt"

run_step "coverage policy" bash scripts/checks/coverage-baseline-schema.sh "$policy"

projects_file="$(mktemp)"
trap 'rm -f "$projects_file"' EXIT
bash scripts/checks/coverage/list-core-projects.sh > "$projects_file"
bash scripts/checks/coverage/matrix.py "$projects_file" >/dev/null

rm -rf "$coverage_root"
mkdir -p "$projects_root"

expected_count=0
while IFS=$'\t' read -r project filter; do
  [[ -n "$project" ]] || continue
  expected_count=$((expected_count + 1))
  run_step "coverage project: $(basename "$(dirname "$project")")" \
    bash scripts/checks/coverage/run-one.sh "$configuration" "$project" "$projects_root" "$filter"
done < "$projects_file"

bash scripts/checks/coverage/find-results.sh "$projects_root" "$expected_count" > "$files_list"
run_step "coverage summary" bash scripts/checks/coverage/summarize.py "$policy" "$coverage_root" "$files_list"
