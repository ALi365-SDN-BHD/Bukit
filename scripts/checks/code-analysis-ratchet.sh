#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

usage() {
  echo "usage: bash scripts/checks/code-analysis-ratchet.sh <check|snapshot OUTPUT>" >&2
}

mode="${1:-}"
case "$mode" in
  check) [[ $# -eq 1 ]] || { usage; exit 2; } ;;
  snapshot) [[ $# -eq 2 ]] || { usage; exit 2; } ;;
  *) usage; exit 2 ;;
esac

baseline="${BUKIT_CODE_ANALYSIS_BASELINE:-scripts/checks/baselines/code-analysis.v1.json}"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-code-analysis-ratchet.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

run_scan() {
  local category="$1" report_dir="$2" log="$3" status=0
  dotnet format "$category" bukit-core.slnx \
    --verify-no-changes \
    --no-restore \
    --severity info \
    --report "$report_dir" \
    --verbosity quiet \
    >"$log" 2>&1 || status=$?
  if [[ "$status" != "0" && "$status" != "2" ]]; then
    echo "gate-error: dotnet format $category failed with exit $status" >&2
    tail -c 4000 "$log" >&2 || true
    return 2
  fi
  if [[ ! -f "$report_dir/format-report.json" ]]; then
    echo "gate-error: dotnet format $category did not produce format-report.json" >&2
    return 2
  fi
}

run_scan style "$scratch/style" "$scratch/style.log"
run_scan analyzers "$scratch/analyzers" "$scratch/analyzers.log"

if [[ "$mode" == "check" ]]; then
  python3 scripts/checks/code-analysis-ratchet.py compare \
    "$baseline" \
    "$scratch/style/format-report.json" \
    "$scratch/analyzers/format-report.json"
else
  python3 scripts/checks/code-analysis-ratchet.py snapshot \
    "$2" \
    "$scratch/style/format-report.json" \
    "$scratch/analyzers/format-report.json"
fi
