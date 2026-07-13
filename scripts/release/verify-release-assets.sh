#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
commit="${2:-}"
asset_dir="${3:-}"

[[ -n "$version" && -n "$commit" && -n "$asset_dir" ]] || {
  echo "usage: bash scripts/release/verify-release-assets.sh <version> <commit> <asset-dir> [expected-rid...]" >&2
  exit 2
}

shift 3
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
exec python3 "$script_dir/release-assets.py" verify \
  "$version" "$commit" "$asset_dir" "$@"
