#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "agent governance contract failed: $*" >&2
  exit 1
}

require_pattern() {
  local path="$1" pattern="$2"
  grep -Eq -- "$pattern" "$path" || fail "$path is missing required pattern: $pattern"
}

reject_pattern() {
  local path="$1" pattern="$2"
  if grep -Eiq -- "$pattern" "$path"; then
    fail "$path contains disallowed workflow detail: $pattern"
  fi
}

require_count() {
  local path="$1" text="$2" expected="$3" actual
  actual="$(grep -Fc -- "$text" "$path" || true)"
  [[ "$actual" == "$expected" ]] ||
    fail "$path expected $expected occurrence(s) of '$text', got $actual"
}

root_rules="AGENTS.md"
workflow="guide/dev/agent-task-workflow.md"
testing="guide/dev/testing.md"

for path in "$root_rules" "$workflow" "$testing"; do
  [[ -f "$path" ]] || fail "required governance file is missing: $path"
done

root_lines="$(wc -l < "$root_rules" | tr -d ' ')"
((root_lines >= 25 && root_lines <= 40)) ||
  fail "AGENTS.md must stay between 25 and 40 lines; got $root_lines"

for heading in \
  "## Scope and precedence" \
  "## Protected reference areas" \
  "## Website/Core isolation" \
  "## Verification boundaries" \
  "## Failure boundary"; do
  grep -Fqx -- "$heading" "$root_rules" || fail "AGENTS.md is missing heading: $heading"
done

require_pattern "$root_rules" 'Nested `AGENTS\.md`.*never weaken'
require_pattern "$root_rules" 'guide-0\.1/.*scripts-0\.2/'
require_pattern "$root_rules" 'src/Bukit-Core/'
require_pattern "$root_rules" 'full/release.*explicit user authorization'
require_pattern "$root_rules" 'post-change-focused\.sh.*changed paths'
require_pattern "$root_rules" 'post-change-targeted\.sh.*parent-base'
require_pattern "$root_rules" 'CI, release, gate, or verification.*owner test/self-test'
require_pattern "$root_rules" 'Environment, permission, tool, or infrastructure.*unrelated code changes'
require_count "$root_rules" 'scripts/checks/post-change-targeted.sh' 1

reject_pattern "$root_rules" 'brainstorming|worktree|test-driven-development|(^|[^A-Za-z])TDD([^A-Za-z]|$)|systematic-debugging|sub-?agents?|code[ -]review|verification-before-completion|pull request|(^|[^A-Za-z])PR([^A-Za-z]|$)|(^|[^A-Za-z])merge([^A-Za-z]|$)|branch cleanup'
reject_pattern "$root_rules" 'After each code subtask.*post-change-targeted'

for heading in \
  "## Superpowers ownership" \
  "### 1. Focused affected checks" \
  "### 2. High-risk stable checkpoint" \
  "### 3. Aggregate parent gate" \
  "## Owner gates and failures"; do
  grep -Fqx -- "$heading" "$workflow" || fail "$workflow is missing heading: $heading"
done
require_pattern "$workflow" 'Superpowers'
require_pattern "$workflow" 'post-change-focused\.sh.*changed paths'
require_pattern "$workflow" 'never runs `ci-fast`'
require_pattern "$workflow" 'post-change-targeted\.sh'
require_pattern "$workflow" 'invokes `ci-fast` exactly once'

for heading in \
  "## Focused affected checks" \
  "## Aggregate targeted gate" \
  "## Direct owner proof paths" \
  "## Explicit broad gates" \
  "## Failure reporting"; do
  grep -Fqx -- "$heading" "$testing" || fail "$testing is missing heading: $heading"
done
require_pattern "$testing" 'post-change-focused\.sh.*changed paths'
require_pattern "$testing" 'does not run `ci-fast`'
require_pattern "$testing" 'post-change-targeted\.sh'
require_pattern "$testing" 'runs `ci-fast`'
require_pattern "$testing" 'exactly once'
require_pattern "$testing" 'explicit user authorization'

echo "agent governance contract OK"
