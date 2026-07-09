#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

run_step "Required guide paths" bash scripts/checks/docs/required-paths.sh
run_step "Core command boundary" bash scripts/checks/docs/no-core-drift.sh
run_step "Local artifact scan" bash scripts/checks/docs/no-local-artifacts.sh
