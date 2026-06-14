#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

if [ "${CI_FULL_SKIP_FAST:-0}" != "1" ]; then
  echo "=== ci-full: fast gate ==="
  bash scripts/gates/ci-fast.sh "$configuration"
fi

echo "=== ci-full: coverage ==="
bash scripts/checks/coverage.sh "$configuration"

echo "=== ci-full: smoke core ==="
SMOKE_SKIP_BUILD=1 bash scripts/smoke/core.sh "$configuration"

echo "=== ci-full: smoke fixtures ==="
bash scripts/smoke/fixtures.sh "$configuration"

echo "=== ci-full: reproducible build ==="
bash scripts/build/build-repro.sh "$configuration"

echo "=== ci-full: security regression ==="
bash scripts/security/security-regression.sh "$configuration"

echo "CI full gate OK"
