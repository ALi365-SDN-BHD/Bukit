#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
coverage_root="${COVERAGE_ROOT:-/tmp/bukit-local-coverage}"
coverage_settings="${COVERAGE_SETTINGS:-coverage.runsettings}"

echo "=== test + coverage ==="
rm -rf "$coverage_root"
mkdir -p "$coverage_root"

dotnet test bukit.slnx \
    -c "$configuration" \
    --collect:"XPlat Code Coverage" \
    --settings "$coverage_settings" \
    --results-directory "$coverage_root"

echo "=== format check ==="
dotnet format bukit.slnx --verify-no-changes --no-restore

echo "=== local quick check OK ==="
