#!/usr/bin/env bash
set -euo pipefail

repo_root() {
  cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd
}

run_step() {
  local label="$1"
  shift
  printf '==> %s\n' "$label"
  "$@"
}
