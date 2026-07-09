#!/usr/bin/env bash
set -euo pipefail

bash scripts/smoke/core.sh "${1:-Release}"
