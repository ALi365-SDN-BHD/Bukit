#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

boundary_script="$PWD/scripts/checks/active-workflow-boundary.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-active-boundary-self-test.XXXXXX")"
output="$scratch.output"
trap 'rm -rf "$scratch" "$output"' EXIT

fail() {
  echo "active workflow boundary self-test failed: $*" >&2
  exit 1
}

mkdir -p "$scratch/.github/workflows" "$scratch/scripts" "$scratch/src" "$scratch/guide/dev"

run_boundary() {
  ACTIVE_WORKFLOW_BOUNDARY_ROOT="$scratch" bash "$boundary_script"
}

run_boundary >/dev/null || fail "clean fixture was rejected"

printf '%s\n' 'A historical `guide-0.2` snapshot informed its information architecture.' > "$scratch/guide/README.md"
printf '%s\n' '- Do not create, synchronize, or modify `guide-0.1/`, `guide-0.2/`,' > "$scratch/guide/dev/agent-task-workflow.md"
printf '%s\n' '  `scripts-0.1/`, or `scripts-0.2/` by default; their absence is valid. Touch' >> "$scratch/guide/dev/agent-task-workflow.md"
run_boundary >/dev/null || fail "narrow policy declarations were rejected"

assert_rejected() {
  local path="$1"
  local content="$2"
  local expected="$3"

  mkdir -p "$(dirname "$scratch/$path")"
  printf '%s\n' "$content" > "$scratch/$path"
  if run_boundary >"$output" 2>&1; then
    fail "$path unexpectedly passed"
  fi
  grep -Fq -- "$expected" "$output" || fail "$path failure did not name $expected"
  rm -f "$scratch/$path"
}

assert_rejected "src/App.cs" 'Run("scripts-0.1/release.sh");' "src/App.cs"
assert_rejected "guide/user/backup.md" '[Old guide](../../guide-0.2/README.md)' "guide/user/backup.md"
assert_rejected "scripts/release.sh" 'source scripts-0.2/release.sh' "scripts/release.sh"
assert_rejected ".github/workflows/ci.yml" 'run: bash scripts-0.1/ci.sh' ".github/workflows/ci.yml"

echo "active workflow boundary self-test OK"
