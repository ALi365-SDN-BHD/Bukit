#!/usr/bin/env bash
set -euo pipefail

bash scripts/gates/release.sh "${1:-Release}"
