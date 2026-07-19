#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

if [ -z "${BUKIT_BIN:-}" ] || [ -z "${BUKIT_SMOKE_ROOT:-}" ]; then
  echo "Set BUKIT_BIN and BUKIT_SMOKE_ROOT for Core smoke validation." >&2
  echo "Example: BUKIT_BIN=./artifacts/bukit BUKIT_SMOKE_ROOT=tests/fixtures/basic-markdown-site bash scripts/smoke/core.sh" >&2
  exit 2
fi

config="${BUKIT_SMOKE_CONFIG:-$BUKIT_SMOKE_ROOT/site.yaml}"
output="${BUKIT_SMOKE_OUTPUT:-dist}"
config_root="$(cd "$(dirname "$config")" && pwd -P)"
config_name="$(basename "$config")"
case "$config_name" in
  [sS][iI][tT][eE].[yY][aA][mM][lL])
    sites_dir="$(dirname "$config_root")"
    case "$(basename "$sites_dir")" in
      [sS][iI][tT][eE][sS]) config_root="$(dirname "$sites_dir")" ;;
    esac
    ;;
esac
audit_output="$config_root/$output"

run_step "smoke config" "$BUKIT_BIN" config check --config "$config"
run_step "smoke build" "$BUKIT_BIN" build --config "$config" --output "$output" --clean
run_step "smoke publish audit" "$BUKIT_BIN" publish audit --dir "$audit_output"
