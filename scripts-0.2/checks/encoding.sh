#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

extensions="\.(md|yaml|yml|json|html|scriban|cs|txt)$"
mojibake_patterns=("绠€" "浣撲" "鈫" "嘳")
found_issues=0

check_file() {
  local f="$1"
  local encoding
  encoding="$(file --mime-encoding --brief "$f" 2>/dev/null || echo "unknown")"
  if [ "$encoding" != "utf-8" ] && [ "$encoding" != "us-ascii" ] && [ "$encoding" != "ascii" ]; then
    echo "ENCODING: $f has non-UTF-8 encoding: $encoding"
    return 1
  fi

  local line=0
  local content_line
  while IFS= read -r content_line; do
    line=$((line + 1))
    for pattern in "${mojibake_patterns[@]}"; do
      if printf '%s\n' "$content_line" | grep -qF "$pattern"; then
        echo "MOJIBAKE: $f:$line contains corrupted characters (pattern: $pattern)"
        echo "  Content: $content_line"
        return 1
      fi
    done
  done < "$f"

  return 0
}

while IFS= read -r -d '' f; do
  check_file "$f" || found_issues=$((found_issues + 1))
done < <(find . -type f -regextype posix-extended -regex ".*${extensions}" \
  -not -path '*/obj/*' \
  -not -path '*/bin/*' \
  -not -path '*/.git/*' \
  -not -path '*/.codex-tmp*/*' \
  -not -path '*/node_modules/*' \
  -not -path '*/.trae/*' \
  -not -path './scripts-0.1/*' \
  -print0 2>/dev/null)

if [ "$found_issues" -gt 0 ]; then
  echo "ERROR: $found_issues file(s) have encoding issues." >&2
  exit 1
fi

echo "Encoding check OK"
