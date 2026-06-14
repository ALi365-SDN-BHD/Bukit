#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

rid="${1:-}"
out_dir="${2:-}"
log_file="${3:-}"

if [ -n "$rid" ] && [ -n "$out_dir" ] && [ -n "$log_file" ]; then
  bash scripts/build/native-aot.sh "$rid" "$out_dir" "$log_file"
elif [ -n "$rid" ] && [ -n "$out_dir" ]; then
  bash scripts/build/native-aot.sh "$rid" "$out_dir"
elif [ -n "$rid" ]; then
  bash scripts/build/native-aot.sh "$rid"
else
  bash scripts/build/native-aot.sh
fi
