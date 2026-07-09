#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

for readme in README.md README.zh-CN.md README.ms.md; do
  for path in guide/user/README.md guide/dev/README.md guide/skills/README.md guide/labs/README.md guide/archive/README.md; do
    rg -q "$path" "$readme" || {
      echo "$readme does not link $path" >&2
      exit 1
    }
  done
done

echo "README sync OK"
