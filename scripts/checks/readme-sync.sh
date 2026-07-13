#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

for readme in README.md README.zh-CN.md README.ms.md; do
  for path in guide/user/README.md guide/dev/README.md guide/skills/README.md guide/labs/README.md guide/archive/README.md; do
    if grep -Fq -- "$path" "$readme"; then
      continue
    else
      grep_status=$?
    fi

    if ((grep_status > 1)); then
      echo "README link text search failed for $readme" >&2
      exit "$grep_status"
    fi

    echo "$readme does not link $path" >&2
    exit 1
  done
done

echo "README sync OK"
