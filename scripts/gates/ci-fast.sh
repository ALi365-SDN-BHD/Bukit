#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

echo "=== checks: file size ==="
bash scripts/checks/file-size.sh

echo "=== checks: repo hygiene ==="
bash scripts/checks/repo-hygiene.sh

echo "=== checks: github action pin compliance ==="
bash scripts/checks/ci-workflow-action-pin.sh

echo "=== checks: encoding ==="
bash scripts/checks/encoding.sh

echo "=== checks: Core CLI script contract ==="
bash scripts/checks/core-cli-contract.sh

echo "=== checks: skills python deps ==="
bash scripts/checks/skills-python-deps.sh

echo "=== checks: skills schema ==="
bash scripts/checks/skills-schema.sh

echo "=== checks: skills strict validation ==="
bash guide/skills/scripts/validate-skills-strict.sh

echo "=== checks: coverage baseline schema ==="
bash scripts/checks/coverage-baseline-schema.sh

echo "=== checks: release assets fixture ==="
bash scripts/release/test-release-assets-fixture.sh

echo "=== checks: workflow evidence fixture ==="
bash scripts/checks/ci-workflow-evidence-fixtures.sh

echo "=== checks: release artifact smoke contract ==="
bash scripts/checks/release-artifact-smoke-contract.sh

echo "=== checks: CLI docs sync ==="
bash scripts/checks/cli-docs-sync.sh

echo "=== checks: CLI docs sync fixtures ==="
bash scripts/checks/test-cli-docs-sync-fixtures.sh

echo "=== restore ==="
dotnet restore bukit.slnx

echo "=== build ==="
dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false

echo "=== test ==="
dotnet test bukit.slnx \
  -c "$configuration" \
  --no-build \
  -maxcpucount:1 \
  -nodeReuse:false \
  --logger trx \
  --results-directory TestResults/ci-fast

echo "=== format ==="
dotnet format bukit.slnx --verify-no-changes --no-restore

echo "=== docs consistency ==="
bash scripts/checks/docs-consistency.sh

echo "=== README sync ==="
bash scripts/checks/readme-sync.sh

echo "CI fast gate OK"
