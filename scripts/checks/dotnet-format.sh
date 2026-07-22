#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

[[ $# -eq 0 ]] || {
  echo "usage: bash scripts/checks/dotnet-format.sh" >&2
  exit 2
}

dotnet format bukit-core.slnx --verify-no-changes --no-restore
