#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

configuration="${CONFIGURATION:-Release}"
rid="${1:-$(bukit_host_rid)}"
out_dir="${2:-TestResults/native-aot/$rid}"
log_file="${3:-TestResults/native-aot/$rid.log}"

if [ "$rid" = "unsupported" ]; then
  echo "ERROR: unsupported host RID for Native AOT publish." >&2
  exit 1
fi

host_rid="$(bukit_host_rid)"
if [ "$host_rid" != "unsupported" ] && [ "$rid" != "$host_rid" ]; then
  case "$rid:$host_rid" in
    linux-*:*|osx-*:*|win-*:*) ;;
  esac
  if [ "${ALLOW_CROSS_HOST_NATIVE_AOT:-0}" != "1" ]; then
    echo "ERROR: Native AOT RID '$rid' must be built on matching host '$host_rid'." >&2
    echo "Set ALLOW_CROSS_HOST_NATIVE_AOT=1 only for environments with the required native toolchain." >&2
    exit 1
  fi
fi

mkdir -p "$(dirname "$log_file")" "$out_dir"
rm -f "$log_file"

dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c "$configuration" \
  -r "$rid" \
  -o "$out_dir" \
  -maxcpucount:1 \
  -nodeReuse:false \
  -p:TrimmerSingleWarn=false \
  2>&1 | tee "$log_file"

warn_lines="$(grep -E "ILC : .*warning IL[0-9]{4}" "$log_file" || true)"
if [ -n "$warn_lines" ]; then
  echo "ERROR: found Native AOT/trim warnings:" >&2
  echo "$warn_lines" >&2
  exit 1
fi

echo "Native AOT publish OK: $rid -> $out_dir"
