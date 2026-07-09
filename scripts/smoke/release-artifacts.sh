#!/usr/bin/env bash
set -euo pipefail

artifact_dir="${1:-}"
[ -n "$artifact_dir" ] || {
  echo "usage: bash scripts/smoke/release-artifacts.sh <artifact-dir>" >&2
  exit 2
}
[ -d "$artifact_dir" ] || {
  echo "missing artifact dir: $artifact_dir" >&2
  exit 1
}
echo "release artifact directory exists"
