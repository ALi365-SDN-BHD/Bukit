#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

configuration="${1:-Release}"

dotnet test tests/Bukit.Config.Tests -c "$configuration" \
  --filter "FullyQualifiedName~ConfigContractDriftTests"
