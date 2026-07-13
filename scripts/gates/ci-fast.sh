#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

run_step "docs consistency" bash scripts/checks/docs-consistency.sh
run_step "active workflow boundary" bash scripts/checks/active-workflow-boundary.sh
run_step "post-change targeted self-test" bash scripts/checks/post-change-targeted-self-test.sh
run_step "ci-fast portability self-test" bash scripts/checks/ci-fast-portability-self-test.sh
run_step "config docs contract" bash scripts/checks/config-docs-contract.sh
run_step "CLI docs sync" bash scripts/checks/cli-docs-sync.sh
run_step "skills schema" bash scripts/checks/skills-schema.sh
run_step "skills strict validation" bash guide/skills/scripts/validate-skills-strict.sh
run_step "README sync" bash scripts/checks/readme-sync.sh
run_step "Core CLI script contract" bash scripts/checks/core-cli-contract.sh
