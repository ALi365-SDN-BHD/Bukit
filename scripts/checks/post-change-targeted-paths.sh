#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/post-change-targeted-paths.sh BASE" >&2; exit 2; }

git diff --name-only "$1" --
git ls-files --others --exclude-standard
