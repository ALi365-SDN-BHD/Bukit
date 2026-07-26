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

root_rules="AGENTS.md"
workflow="guide/dev/agent-task-workflow.md"
testing="guide/dev/testing.md"
workflow_tool="scripts/checks/codex-workflow.py"
workflow_policy="scripts/checks/codex-workflow-policy.v1.json"
workflow_self_test="scripts/checks/codex-workflow-self-test.sh"

for path in \
  "$root_rules" "$workflow" "$testing" \
  "$workflow_tool" "$workflow_policy" "$workflow_self_test"; do
  [[ -f "$path" ]] || fail "required governance file is missing: $path"
done

for heading in \
  "## Scope and precedence" \
  "## Protected reference areas" \
  "## Website/Core isolation" \
  "## Verification boundaries" \
  "## High-speed agent workflow" \
  "## Failure boundary"; do
  grep -Fqx -- "$heading" "$root_rules" || fail "AGENTS.md is missing heading: $heading"
done

require_pattern "$root_rules" 'Nested `AGENTS\.md`.*never weaken'
require_pattern "$root_rules" 'guide-0\.1/.*scripts-0\.2/'
require_pattern "$root_rules" 'src/Bukit-Core/'
require_pattern "$root_rules" 'full/release.*explicit user authorization'
require_pattern "$root_rules" 'codex-workflow\.py closure'
require_pattern "$root_rules" 'codex-workflow\.py cache record'
require_pattern "$root_rules" 'HEAD.*verification-closure content.*exact test command.*environment-variable state.*SDK/toolchain'
require_pattern "$root_rules" 'codex-workflow\.py classify'
require_pattern "$root_rules" 'static-parallel.*dotnet-serial.*fixture-exclusive'
require_pattern "$root_rules" 'CI, release, gate, or verification.*owner test/self-test'
require_pattern "$root_rules" 'only one implementation agent'
require_pattern "$root_rules" 'codex-workflow\.py queue init'
require_pattern "$root_rules" 'one specialty review'
require_pattern "$root_rules" 'Critical or Important'
require_pattern "$root_rules" 'review-scope.*delta-only unified review'
require_pattern "$root_rules" 'codex-workflow\.py metrics add'
require_pattern "$root_rules" 'same error occurs twice'
require_pattern "$root_rules" 'no progress for 90 seconds'
require_pattern "$root_rules" 'Environment, permission, tool, or infrastructure.*unrelated code changes'
reject_pattern "$root_rules" 'After each code subtask.*post-change-(focused|targeted)'

for heading in \
  "## Superpowers ownership" \
  "### 1. Generate the verification closure" \
  "### 2. Record and reuse GREEN evidence" \
  "### 3. Specialty review" \
  "### 4. Delta-only final review" \
  "## Single-writer queue" \
  "## Speed metrics" \
  "## Owner gates and failures"; do
  grep -Fqx -- "$heading" "$workflow" || fail "$workflow is missing heading: $heading"
done
require_pattern "$workflow" 'Superpowers'
require_pattern "$workflow" 'codex-workflow\.py closure'
require_pattern "$workflow" 'codex-workflow\.py cache record'
require_pattern "$workflow" 'cross-task file intersections'
require_pattern "$workflow" 'Minor findings'
require_pattern "$workflow" 'do not expand the final review scope'
require_pattern "$workflow" 'codex-workflow\.py queue acquire'
require_pattern "$workflow" 'writing.*testing.*review_wait'
require_pattern "$workflow" 'codex-workflow\.py metrics add'
require_pattern "$workflow" 'never pass the raw command'

for heading in \
  "## Verification closure" \
  "## GREEN evidence cache" \
  "## Resource classification" \
  "## Final review scope" \
  "## Direct owner proof paths" \
  "## Explicit broad gates" \
  "## Failure reporting"; do
  grep -Fqx -- "$heading" "$testing" || fail "$testing is missing heading: $heading"
done
require_pattern "$testing" 'codex-workflow\.py closure'
require_pattern "$testing" 'codex-workflow\.py cache record'
require_pattern "$testing" 'codex-workflow-self-test\.sh'
require_pattern "$testing" 'codex-workflow\.py classify'
require_pattern "$testing" 'static-parallel.*commands may'
require_pattern "$testing" 'dotnet-serial'
require_pattern "$testing" 'fixture-exclusive'
require_pattern "$testing" 'No `post-change-\*`'
require_pattern "$testing" 'unnamed gate is routine'
require_pattern "$testing" 'explicit user authorization'

echo "agent governance contract OK"
