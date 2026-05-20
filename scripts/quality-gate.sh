#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false
dotnet test bukit.slnx -c "$configuration" --no-build -maxcpucount:1 -nodeReuse:false
dotnet format bukit.slnx --verify-no-changes --no-restore
bash scripts/check-doc-asset-consistency.sh
bash scripts/smoke.sh "$configuration"

echo "Quality gate OK"
