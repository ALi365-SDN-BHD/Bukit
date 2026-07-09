#!/usr/bin/env bash
set -euo pipefail

bash "$(dirname "${BASH_SOURCE[0]}")/ci-fast.sh" "${1:-Release}"
echo "Release gate here is intentionally thin; run release artifact validation explicitly when publishing binaries."
