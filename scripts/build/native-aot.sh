#!/usr/bin/env bash
set -euo pipefail

[[ $# -ge 3 && $# -le 4 ]] || {
  echo "usage: bash scripts/build/native-aot.sh <version> <rid> <output-root> [configuration]" >&2
  exit 2
}

version="$1"
rid="$2"
output_root="$3"
configuration="${4:-Release}"

printf 'Native AOT package: version=%s rid=%s configuration=%s\n' \
  "$version" "$rid" "$configuration" >&2
exec bash "$(dirname "${BASH_SOURCE[0]}")/package-native-aot.sh" \
  "$version" "$rid" "$output_root" "$configuration"
