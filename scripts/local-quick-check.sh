#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

CI_FULL_SKIP_FAST=0 COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-65}" bash scripts/gates/ci-fast.sh "${1:-Release}"
