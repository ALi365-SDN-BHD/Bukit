#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== restore ==="
dotnet restore bukit.slnx

echo "=== build ==="
dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false

echo "=== unit tests ==="
dotnet test bukit.slnx -c "$configuration" --no-build -maxcpucount:1 -nodeReuse:false

echo "=== quality gate ==="
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-65}" bash scripts/quality-gate.sh "$configuration"

echo "=== example smoke ==="
bash scripts/smoke.sh "$configuration"
bash scripts/smoke-all.sh "$configuration"

echo "=== AOT publish ==="
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c "$configuration" -p:PublishAot=true

echo "=== test all OK ==="