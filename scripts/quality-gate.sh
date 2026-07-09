#!/usr/bin/env bash
set -euo pipefail

bash scripts/gates/ci-fast.sh "${1:-Release}"
