#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
commit="${2:-}"
output_dir="${3:-}"

[[ -n "$version" && -n "$commit" && -n "$output_dir" && $# -gt 3 ]] || {
  echo "usage: bash scripts/release/prepare-release-assets.sh <version> <commit> <output-dir> <archive>..." >&2
  exit 2
}

shift 3
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec python3 "$script_dir/release-assets.py" prepare \
  "$version" "$commit" "$output_dir" "$@"
