#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

configuration="${1:?configuration is required}"
project="${2:?project is required}"
output_root="${3:?output root is required}"
filter="${4:-}"
settings="${BUKIT_COVERAGE_SETTINGS:-coverage.runsettings}"
name="$(basename "$(dirname "$project")")"

output_root="$(bash scripts/checks/coverage/validate-output-root.py "$output_root" "$repo_root")"
results_dir="$(bash scripts/checks/coverage/validate-output-root.py "${output_root}/${name}" "$repo_root")"

echo "coverage project: ${name}"
echo "coverage output: ${results_dir}"
rm -rf "$results_dir"
mkdir -p "$results_dir"

args=(
  "$project"
  -c "$configuration"
  "--collect:XPlat Code Coverage"
  --settings "$settings"
  --logger "console;verbosity=minimal"
  --results-directory "$results_dir"
)

if [[ -n "$filter" ]]; then
  args+=(--filter "$filter")
fi

dotnet test "${args[@]}"
