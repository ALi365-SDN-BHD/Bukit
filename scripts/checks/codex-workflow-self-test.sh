#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

tool=(python3 scripts/checks/codex-workflow.py)
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-codex-workflow.XXXXXX")"
lock_holder_pid=""

cleanup() {
  if [[ -n "$lock_holder_pid" ]] && kill -0 "$lock_holder_pid" 2>/dev/null; then
    kill -KILL "$lock_holder_pid" 2>/dev/null || true
    wait "$lock_holder_pid" 2>/dev/null || true
  fi
  rm -rf "$scratch"
}
trap cleanup EXIT

fail() {
  echo "codex workflow self-test failed: $*" >&2
  exit 1
}

assert_contains() {
  case "$1" in *"$2"*) ;; *) fail "expected output to contain: $2" ;; esac
}

expect_exit() {
  local expected="$1"
  shift
  set +e
  command_output="$("$@" 2>&1)"
  command_status=$?
  set -e
  [[ "$command_status" == "$expected" ]] ||
    fail "expected exit $expected, got $command_status: $command_output"
}

assert_closure_mapping() {
  local repo="$1"
  local changed="$2"
  local expected_commands_json="$3"
  local expected_public_contract="$4"

  expect_exit 0 "${tool[@]}" closure \
    --repo "$repo" \
    --policy scripts/checks/codex-workflow-policy.v1.json \
    --changed "$changed"

  python3 - "$command_output" "$changed" "$expected_commands_json" "$expected_public_contract" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
changed = sys.argv[2]
expected_commands = json.loads(sys.argv[3])
expected_public_contract = sys.argv[4] == "true"

if changed in result["unmappedFiles"]:
    raise SystemExit(f"expected mapped closure path, got unmapped: {changed}")
if result["specialtyTests"] != expected_commands:
    raise SystemExit(
        f"unexpected specialty tests for {changed}: {result['specialtyTests']}"
    )
expected_contract_files = [changed] if expected_public_contract else []
if result["publicContractFiles"] != expected_contract_files:
    raise SystemExit(
        f"unexpected public contract files for {changed}: "
        f"{result['publicContractFiles']}"
    )
PY
}


self_test_parts="scripts/checks/codex-workflow-self-test.d"
source "$self_test_parts/cache.sh"
source "$self_test_parts/closure-fixture.sh"
source "$self_test_parts/closure-basic.sh"
source "$self_test_parts/closure-projects.sh"
source "$self_test_parts/closure-packages.sh"
source "$self_test_parts/review.sh"
source "$self_test_parts/queue.sh"
source "$self_test_parts/classification.sh"
source "$self_test_parts/metrics.sh"

echo "codex workflow self-test OK"
