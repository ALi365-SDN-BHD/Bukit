#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
if [[ "${BUKIT_CI_FULL_SKIP_FAST:-0}" != "1" ]]; then
  run_step "fast contract gate" bash scripts/gates/ci-fast.sh "$configuration"
fi
run_step "Core test projects" bash scripts/checks/core-tests.sh "$configuration"
