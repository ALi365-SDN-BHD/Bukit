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

dotnet build bukit-core.slnx -c "$configuration" --no-restore --nologo >&2

tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c "$configuration" --no-build --no-restore --)
if [[ "$mode" == "check" ]]; then
  "${tool[@]}" check "$baseline" "$root" "$configuration"
else
  "${tool[@]}" snapshot "$baseline" "$output" "$root" "$configuration"
fi
