#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

forbidden_re='bukit[[:space:]]+(docs|intent|plugin|theme|import|clone|visual|webhook|data)([[:space:]]|$)|docs[[:space:]]+check|--allow-external-plugins'
violations=0

is_allowed_contract_guard() {
  local file="$1"
  local match="$2"

  case "$file" in
    scripts/smoke/release-artifacts.sh)
      # The release artifact smoke stores this pattern only to assert that
      # published CLI help does not expose non-Core commands or flags.
      case "$match" in
        *'non_core_help_re='*) return 0 ;;
      esac
      ;;
  esac

  return 1
}

scan_file() {
  local file="$1"
  case "$file" in
    scripts/checks/core-cli-contract.sh) return 0 ;;
  esac

  if grep -nE "$forbidden_re" "$file" >/tmp/bukit-core-contract-match.$$ 2>/dev/null; then
    local file_has_violations=0

    while IFS= read -r match; do
      if is_allowed_contract_guard "$file" "$match"; then
        continue
      fi

      if [ "$file_has_violations" -eq 0 ]; then
        echo "ERROR: forbidden non-Core CLI usage in $file" >&2
        file_has_violations=1
      fi

      echo "  $match" >&2
    done </tmp/bukit-core-contract-match.$$

    if [ "$file_has_violations" -ne 0 ]; then
      violations=1
    fi
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
