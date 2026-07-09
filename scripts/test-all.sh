#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
run_step "all repository tests" dotnet test bukit-test.slnx -c "$configuration"
