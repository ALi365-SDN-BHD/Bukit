#!/usr/bin/env bash
set -euo pipefail

repo_root="${ACTIVE_WORKFLOW_BOUNDARY_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)}"
cd "$repo_root"

targets=(scripts src guide)
if [[ -d .github/workflows ]]; then
  targets=(.github/workflows scripts src guide)
fi

forbidden=(
  ".github/workflows-0.1"
  "workflows-0.1"
  "scripts-0.1"
  "scripts-0.2"
  "guide-0.1"
  "guide-0.2"
)

is_allowed_policy_reference() {
  local path="$1"
  local text="$2"

  if [[ "$path" == "guide/README.md" ]]; then
    [[ "$text" == *'`guide-0.2` snapshot informed its information architecture'* ]] && return 0
    [[ "$text" == 'If present, `guide-0.1`, `guide-0.2`, `scripts-0.1`, and `scripts-0.2` are' ]] && return 0
  fi

  if [[ "$path" == "guide/dev/agent-task-workflow.md" ]]; then
    [[ "$text" == '- Do not create, synchronize, or modify `guide-0.1/`, `guide-0.2/`,' ]] && return 0
    [[ "$text" == '  `scripts-0.1/`, or `scripts-0.2/` by default; their absence is valid. Touch' ]] && return 0
  fi

  return 1
}

failed=0
for pattern in "${forbidden[@]}"; do
  grep_status=0
  matches="$(grep -RInE \
    --exclude='active-workflow-boundary.sh' \
    --exclude='active-workflow-boundary-self-test.sh' \
    --exclude-dir=bin \
    --exclude-dir=obj \
    -- "$pattern" "${targets[@]}")" || grep_status=$?
  if ((grep_status > 1)); then
    echo "Active workflow boundary text search failed" >&2
    exit "$grep_status"
  fi
  violations=""
  while IFS= read -r match; do
    [[ -n "$match" ]] || continue
    path="${match%%:*}"
    remainder="${match#*:}"
    text="${remainder#*:}"
    if ! is_allowed_policy_reference "$path" "$text"; then
      violations+="$match"$'\n'
    fi
  done <<< "$matches"
  if [[ -n "$violations" ]]; then
    printf 'backup/reference path referenced from an active repository surface: %s\n' "$pattern" >&2
    printf '%s' "$violations" >&2
    failed=1
  fi
done

if [[ "$failed" -ne 0 ]]; then
  exit 1
fi

echo "Active workflow boundary OK"
