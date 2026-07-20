#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
root="$(repo_root)"
cd "$root"

usage() { echo "usage: bash scripts/checks/public-api-drift.sh <check [Configuration]|snapshot OUTPUT [Configuration]>" >&2; }
[[ $# -ge 1 ]] || { usage; exit 2; }

mode="$1"
shift
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
case "$mode" in
  check)
    [[ $# -le 1 ]] || { usage; exit 2; }
    configuration="${1:-Release}"
    ;;
  snapshot)
    [[ $# -ge 1 && $# -le 2 && -n "$1" ]] || { usage; exit 2; }
    output="$1"
    configuration="${2:-Release}"
    ;;
  *)
    usage
    exit 2
    ;;
esac

build_log="$(mktemp "${TMPDIR:-/tmp}/bukit-public-api-build.XXXXXX")"
trap 'rm -f -- "$build_log"' EXIT
if dotnet build bukit-core.slnx -c "$configuration" --no-restore --nologo >"$build_log" 2>&1; then
  /bin/cat "$build_log" >&2
else
  build_status=$?
  build_detail="$(head -c 400 "$build_log" | tr '\r\n' '  ')"
  printf 'gate-error: dotnet build --no-restore failed (exit %s): %s\n' "$build_status" "$build_detail" >&2
  exit 2
fi

tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c "$configuration" --no-build --no-restore --)
if [[ "$mode" == "check" ]]; then
  "${tool[@]}" check "$baseline" "$root" "$configuration"
else
  "${tool[@]}" snapshot "$baseline" "$output" "$root" "$configuration"
fi
