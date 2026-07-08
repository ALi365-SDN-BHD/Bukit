#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

bash scripts/gates/ci-full.sh "$configuration"

if [ "${TEST_ALL_SKIP_NATIVE_AOT:-0}" != "1" ]; then
  CONFIGURATION="$configuration" bash scripts/build/native-aot.sh
fi

echo "test-all OK"
