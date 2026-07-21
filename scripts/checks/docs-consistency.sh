#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

run_step "Required guide paths" bash scripts/checks/docs/required-paths.sh
run_step "Agent governance contract" bash scripts/checks/agent-governance-contract.sh
run_step "Public documentation contracts" bash scripts/checks/docs/public-doc-contracts.sh
run_step "Public absolute path scan" bash scripts/checks/docs/no-absolute-paths.sh
run_step "Active documentation links" bash scripts/checks/docs/active-links.sh
run_step "Active size policy self-test" bash scripts/checks/docs/size-policy-self-test.sh
run_step "Active size policy" bash scripts/checks/docs/size-policy.sh
run_step "Core command boundary" bash scripts/checks/docs/no-core-drift.sh
run_step "Local artifact scan" bash scripts/checks/docs/no-local-artifacts.sh
