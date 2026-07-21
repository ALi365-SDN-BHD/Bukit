#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

script="scripts/checks/post-change-focused-owner-checks.sh"
output="$(mktemp "${TMPDIR:-/tmp}/bukit-focused-owner-checks.XXXXXX")"
trap 'rm -f "$output"' EXIT

fail() {
  echo "post-change focused owner-checks self-test failed: $*" >&2
  exit 1
}

assert_contains() {
  case "$1" in *"$2"*) ;; *) fail "expected output to contain: $2" ;; esac
}

assert_not_contains() {
  case "$1" in *"$2"*) fail "unexpected output contains: $2" ;; esac
}

assert_count() {
  local actual
  actual="$(printf '%s\n' "$1" | awk -v needle="$2" '
    { line = $0; while ((at = index(line, needle)) > 0) { count++; line = substr(line, at + length(needle)); } }
    END { print count + 0 }
  ')"
  [[ "$actual" == "$3" ]] || fail "expected $3 occurrence(s) of: $2; got $actual"
}

bash "$script" --dry-run

out="$(bash "$script" --dry-run -- AGENTS.md guide/dev/agent-task-workflow.md)"
assert_count "$out" "bash scripts/checks/agent-governance-contract.sh" 1

out="$(bash "$script" --dry-run -- guide/skills/AGENTS.md)"
assert_count "$out" "bash guide/skills/scripts/validate-skills-strict.sh" 1
assert_not_contains "$out" "bash scripts/checks/agent-governance-contract.sh"

if grep -Fq -- 'guide/skills/AGENTS.md' scripts/checks/agent-governance-contract.sh; then
  fail "central agent governance contract unexpectedly owns the Skills pack rules"
fi

out="$(bash "$script" --dry-run -- scripts/checks/post-change-targeted.sh)"
assert_contains "$out" "bash scripts/checks/post-change-targeted-self-test.sh"

out="$(bash "$script" --dry-run -- scripts/gates/ci-fast.sh scripts/quality-gate.sh)"
assert_count "$out" "bash scripts/checks/ci-fast-portability-self-test.sh" 1
assert_not_contains "$out" "bash scripts/gates/ci-fast.sh"

out="$(bash "$script" --dry-run -- .github/workflows/ci.yaml)"
assert_contains "$out" "bash scripts/checks/active-workflow-boundary-self-test.sh"
assert_contains "$out" "bash scripts/checks/active-workflow-boundary.sh"

out="$(bash "$script" --dry-run -- \
  scripts/security/security-regression.sh scripts/security/security-regression-self-test.sh)"
assert_count "$out" "bash scripts/security/security-regression-self-test.sh" 1

out="$(bash "$script" --dry-run -- \
  scripts/smoke/release-artifacts.sh scripts/smoke/release-artifacts-self-test.sh)"
assert_count "$out" "bash scripts/smoke/release-artifacts-self-test.sh" 1

out="$(bash "$script" --dry-run -- \
  scripts/release/release-assets.py scripts/release/release-assets-self-test.sh)"
assert_count "$out" "bash scripts/release/release-assets-self-test.sh" 1

out="$(bash "$script" --dry-run -- scripts/build/native-aot.sh scripts/build/package-native-aot.sh)"
assert_count "$out" "bash scripts/build/native-aot-self-test.sh" 1

out="$(bash "$script" --dry-run -- scripts/lib/common.sh)"
assert_contains "$out" "bash scripts/checks/post-change-focused-self-test.sh"
assert_contains "$out" "bash scripts/checks/post-change-targeted-self-test.sh"
assert_contains "$out" "bash scripts/checks/ci-fast-portability-self-test.sh"

out="$(bash "$script" --dry-run -- \
  guide/skills/scripts/validate-skills-strict.sh guide/skills/scripts/check-cli-commands.py)"
assert_count "$out" "bash guide/skills/scripts/validate-skills-strict.sh" 1

if bash "$script" --dry-run -- scripts/security/no-such-owner.py >"$output" 2>&1; then
  fail "unknown security owner path unexpectedly passed"
fi
assert_contains "$(cat "$output")" "No focused owner check registered"

if bash "$script" --dry-run -- scripts/lib/no-such-owner.sh >"$output" 2>&1; then
  fail "unknown shared gate helper unexpectedly passed"
fi
assert_contains "$(cat "$output")" "No focused owner check registered"

echo "post-change focused owner-checks self-test OK"
