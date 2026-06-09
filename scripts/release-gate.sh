#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== release gate: test all ==="
bash scripts/test-all.sh "$configuration"

echo "=== release gate: security regression ==="
bash scripts/security-regression.sh "$configuration"

echo "=== release gate: AOT zero warning ==="
bash scripts/check-aot-warnings.sh linux-x64

echo "=== release gate: docs check ==="
dotnet run --project src/Bukit.Cli -c "$configuration" -- docs check

echo "=== release gate: config schema ==="
dotnet run --project src/Bukit.Cli -c "$configuration" -- config schema --output /tmp/bukit-site.schema.json

echo "=== release gate OK ==="