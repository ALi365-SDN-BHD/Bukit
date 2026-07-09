#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

if [ -z "${BUKIT_BIN:-}" ] || [ -z "${BUKIT_SMOKE_ROOT:-}" ]; then
  echo "Set BUKIT_BIN and BUKIT_SMOKE_ROOT for Core smoke validation." >&2
  echo "Example: BUKIT_BIN=./artifacts/bukit BUKIT_SMOKE_ROOT=examples/minimal bash scripts/smoke/core.sh" >&2
  exit 2
fi

config="${BUKIT_SMOKE_CONFIG:-$BUKIT_SMOKE_ROOT/site.yaml}"
output="${BUKIT_SMOKE_OUTPUT:-$BUKIT_SMOKE_ROOT/dist}"

run_step "smoke config" "$BUKIT_BIN" config check --config "$config"
run_step "smoke build" "$BUKIT_BIN" build --config "$config" --output "$output" --clean
run_step "smoke publish audit" "$BUKIT_BIN" publish audit --dir "$output"
