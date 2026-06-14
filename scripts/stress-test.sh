#!/usr/bin/env bash
set -euo pipefail

runs="${1:-20}"
configuration="${2:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

for i in $(seq 1 "$runs"); do
  echo "=== stress run $i / $runs ==="
  dotnet test bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false
done

echo "Stress test OK"
