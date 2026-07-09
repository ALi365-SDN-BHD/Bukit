#!/usr/bin/env bash
set -euo pipefail

required=(
  README.md
  README.zh-CN.md
  README.ms.md
  guide/README.md
  guide/user/README.md
  guide/dev/README.md
  guide/skills/README.md
  guide/labs/README.md
  guide/labs-skills/README.md
  guide/archive/README.md
  guide/user/12-cli-reference.md
  guide/user/13-deploy-github-pages.md
)

for path in "${required[@]}"; do
  [ -e "$path" ] || {
    echo "missing required path: $path" >&2
    exit 1
  }
done

echo "required guide paths OK"
