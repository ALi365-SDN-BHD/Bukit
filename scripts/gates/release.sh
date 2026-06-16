#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

artifact_dir="${RELEASE_GATE_ARTIFACT_DIR:-TestResults/release-gate}"
rid_list="${RELEASE_GATE_RIDS:-$(bukit_host_rid)}"
required_branches="${RELEASE_GATE_REQUIRED_BRANCHES:-main,master}"
require_branch_filter="${RELEASE_GATE_REQUIRE_BRANCH_FILTER:-1}"

if is_truthy "${require_branch_filter}"; then
  case "${GITHUB_EVENT_NAME:-}" in
    workflow_dispatch)
      require_branch_filter="0"
      ;;
    push)
      case "${GITHUB_REF:-}" in
        refs/tags/*)
          require_branch_filter="0"
          ;;
      esac
      ;;
  esac
fi

if ! is_truthy "${require_branch_filter}"; then
  required_branches=""
fi

echo "=== checks: github action pin compliance ==="
bash scripts/checks/ci-workflow-action-pin.sh

echo "=== release: full gate ==="
COVERAGE_SUMMARY_FILE="${artifact_dir}/coverage-summary.txt" bash scripts/gates/ci-full.sh "$configuration"

if is_truthy "${GITHUB_ACTIONS:-0}"; then
  echo "=== release: workflow evidence check ==="
  bash scripts/checks/ci-workflow-evidence.sh "${GITHUB_REPOSITORY}" "${GITHUB_SHA}" "ci.yml" "$artifact_dir/ci-workflow-evidence.json" 1 "$artifact_dir/rc-gate-evidence.md" "$required_branches"
  test -s "$artifact_dir/ci-workflow-evidence.json"
  test -s "$artifact_dir/rc-gate-evidence.md"
fi

echo "=== release: config schema artifact ==="
mkdir -p "$artifact_dir"
bukit_cli "$configuration" config schema --output "$artifact_dir/site.schema.json"
test -s "$artifact_dir/site.schema.json"
python3 -m json.tool "$artifact_dir/site.schema.json" >/dev/null

echo "=== release: Native AOT artifacts ==="
for rid in $rid_list; do
  out_dir="$artifact_dir/native-aot/$rid"
  rm -rf "$out_dir"
  mkdir -p "$out_dir"
  CONFIGURATION="$configuration" bash scripts/build/native-aot.sh "$rid" "$out_dir" "$artifact_dir/native-aot/$rid.log"
  bash scripts/smoke/release-artifacts.sh "$artifact_dir/native-aot/$rid"
done

echo "Release gate OK"
