#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_dir="${repo_root}/tests/fixtures/workflow-evidence"
tmp_root="$(mktemp -d)"
repo="ALi365-SDN-BHD/Bukit"
sha="0123456789abcdef0123456789abcdef01234567"
workflow="ci.yml"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

run_case() {
  local fixture="$1"
  local require_success="$2"
  local required_branches="$3"
  local case_name="$4"

  python3 "${repo_root}/scripts/checks/ci-workflow-evidence-evaluate.py" \
    "${fixture_dir}/${fixture}.json" \
    "$repo" \
    "$sha" \
    "$workflow" \
    "$require_success" \
    "$required_branches" \
    "${tmp_root}/${case_name}.json" \
    "${tmp_root}/${case_name}.md" \
    "fixture://${fixture}" \
    >"${tmp_root}/${case_name}.out" \
    2>"${tmp_root}/${case_name}.err"
}

expect_pass() {
  local fixture="$1"
  local require_success="$2"
  local required_branches="$3"
  local case_name="$4"

  if ! run_case "$fixture" "$require_success" "$required_branches" "$case_name"; then
    echo "ERROR: workflow evidence fixture should have passed: $case_name" >&2
    cat "${tmp_root}/${case_name}.out" >&2
    cat "${tmp_root}/${case_name}.err" >&2
    exit 1
  fi

  grep -Fq "Decision | **PASS**" "${tmp_root}/${case_name}.md"
  python3 -m json.tool "${tmp_root}/${case_name}.json" >/dev/null
  echo "workflow evidence fixture passed: $case_name"
}

expect_fail() {
  local fixture="$1"
  local require_success="$2"
  local required_branches="$3"
  local case_name="$4"
  local expected="$5"

  if run_case "$fixture" "$require_success" "$required_branches" "$case_name"; then
    echo "ERROR: workflow evidence fixture should have failed: $case_name" >&2
    cat "${tmp_root}/${case_name}.out" >&2
    exit 1
  fi

  grep -Fq "$expected" "${tmp_root}/${case_name}.err"
  grep -Fq "Decision | **BLOCKED**" "${tmp_root}/${case_name}.md"
  python3 -m json.tool "${tmp_root}/${case_name}.json" >/dev/null
  echo "workflow evidence fixture failed as expected: $case_name"
}

expect_pass "success-main" "1" "main" "success-main"
expect_pass "success-master" "1" "master" "success-master"
expect_pass "success-main" "1" "main,master" "success-main-with-required-main-master"
expect_pass "success-master" "1" "main,master" "success-master-with-required-main-master"
expect_pass "multiple-runs-latest-failed-older-success" "1" "main" "latest-failed-older-success"
expect_fail "success-feature-only" "1" "main,master" "success-feature-only" "no completed successful workflow runs on required branch(es): main, master"
expect_fail "failed-main" "1" "main" "failed-main" "no completed successful workflow runs on required branch(es): main"
expect_fail "cancelled-main" "1" "main" "cancelled-main" "no completed successful workflow runs on required branch(es): main"
expect_fail "no-runs" "1" "main" "no-runs-require-success" "no completed successful workflow runs on required branch(es): main"
expect_pass "failed-main" "0" "main" "require-success-false-with-run"
expect_fail "no-runs" "0" "main" "require-success-false-no-runs" "commit has no workflow runs"

echo "workflow evidence fixture test OK"
