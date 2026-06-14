#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

forbidden_re='bukit[[:space:]]+(docs|intent|plugin|theme|import|clone|visual|webhook|data)([[:space:]]|$)|docs[[:space:]]+check|--allow-external-plugins'
violations=0

scan_file() {
  local file="$1"
  case "$file" in
    scripts/checks/core-cli-contract.sh) return 0 ;;
  esac

  if grep -nE "$forbidden_re" "$file" >/tmp/bukit-core-contract-match.$$ 2>/dev/null; then
    echo "ERROR: forbidden non-Core CLI usage in $file" >&2
    sed 's/^/  /' /tmp/bukit-core-contract-match.$$ >&2
    violations=1
  fi
}

if [ -d scripts ]; then
  while IFS= read -r -d '' file; do
    scan_file "${file#./}"
  done < <(find scripts -type f -print0)
fi

if [ -d .github/workflows ]; then
  while IFS= read -r -d '' file; do
    scan_file "${file#./}"
  done < <(find .github/workflows -type f -print0)
fi

rm -f /tmp/bukit-core-contract-match.$$

if [ "$violations" -ne 0 ]; then
  exit 1
fi

echo "Core CLI script contract OK"
