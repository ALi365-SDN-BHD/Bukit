#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "agent governance contract failed: $*" >&2
  exit 1
}

require_text() {
  local path="$1"
  local expected="$2"
  grep -Fq -- "$expected" "$path" || fail "$path is missing required text: $expected"
}

reject_text() {
  local path="$1"
  local rejected="$2"
  if grep -Fq -- "$rejected" "$path"; then
    fail "$path contains obsolete text: $rejected"
  fi
}

root_rules="AGENTS.md"
workflow="guide/dev/agent-task-workflow.md"
testing="guide/dev/testing.md"
skills_rules="guide/skills/AGENTS.md"

for path in "$root_rules" "$workflow" "$testing" "$skills_rules"; do
  [[ -f "$path" ]] || fail "required governance file is missing: $path"
done

reject_text "$root_rules" "Rule-definition and rule-modification tasks do not require a repository gate."

require_text "$root_rules" "### Applicability and precedence"
require_text "$root_rules" 'This root `AGENTS.md` applies to the entire repository.'
require_text "$root_rules" 'A nested `AGENTS.md` applies only to its directory and descendants.'
require_text "$root_rules" "Higher-priority platform instructions and explicit user instructions take"
require_text "$root_rules" "Rule-definition and rule-modification tasks do not require runtime, full, or"
require_text "$root_rules" 'git diff --check -- <changed governance paths>'
require_text "$root_rules" 'bash scripts/checks/docs-consistency.sh'
require_text "$root_rules" 'bash scripts/checks/skills-schema.sh'
require_text "$root_rules" 'bash guide/skills/scripts/validate-skills-strict.sh'
require_text "$root_rules" "The docs-consistency gate owns this governance contract."
require_text "$root_rules" "The user may explicitly cancel, replace, pause, or request an interim handoff"
require_text "$root_rules" "task without explicit user redirection."

require_text "$workflow" "## Lifecycle exits"
require_text "$workflow" "## Rule applicability and precedence"
require_text "$workflow" 'The root `AGENTS.md` applies repository-wide.'
require_text "$workflow" 'A nested `AGENTS.md` applies only'
require_text "$workflow" "The user may explicitly cancel, replace, pause, or request an interim handoff"
require_text "$workflow" "without explicit user redirection."

for path in "$workflow" "$testing"; do
  require_text "$path" "## Rule-change verification"
  require_text "$path" "Rule-definition and rule-modification tasks do not require runtime, full, or"
  require_text "$path" 'git diff --check -- <changed-governance-paths>'
  require_text "$path" 'bash scripts/checks/docs-consistency.sh'
  require_text "$path" 'bash scripts/checks/skills-schema.sh'
  require_text "$path" 'bash guide/skills/scripts/validate-skills-strict.sh'
done

require_text "$skills_rules" 'For rule changes under `guide/skills/` or changes to this nested `AGENTS.md`,'
require_text "$skills_rules" 'This file applies only to `guide/skills/` and its descendants.'
require_text "$skills_rules" 'the root `AGENTS.md` and may add stricter requirements'
require_text "$skills_rules" 'bash scripts/checks/docs-consistency.sh'
require_text "$skills_rules" 'bash scripts/checks/skills-schema.sh'
require_text "$skills_rules" 'bash guide/skills/scripts/validate-skills-strict.sh'

echo "agent governance contract OK"
