#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

ERROR_COUNT=0
WARN_COUNT=0

RULES=(
  "src/BukitJalil"
  "BukitJalil.slnx"
  "tools/ImageSharp"
  ".github/workflows/smoke.yml"
  ".github/workflows/build.yaml"
)

ALLOW_KEYWORDS=(
  "示例"
  "需自建"
  "参考"
  "自行创建"
  "自行在"
  "自建"
  "example"
  "examples"
  "reference"
  "create your own"
  "rg -n"
)

REQUIRED_PATHS=(
  "bukit.slnx"
  "guide/dev"
  "guide/user"
)

EXTRA_PATHS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --extra-path)
      shift
      if [[ $# -eq 0 ]]; then
        echo "ERROR: --extra-path requires a value"
        exit 1
      fi
      EXTRA_PATHS+=("$1")
      ;;
    *)
      echo "WARN: unknown argument '$1' ignored"
      ;;
  esac
  shift
done

doc_error() {
  ERROR_COUNT=$((ERROR_COUNT + 1))
  echo "ERROR: $1"
}

doc_warn() {
  WARN_COUNT=$((WARN_COUNT + 1))
  echo "WARN: $1"
}

is_allowed_context() {
  local line_lower
  line_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  for keyword in "${ALLOW_KEYWORDS[@]}"; do
    local keyword_lower
    keyword_lower="$(printf '%s' "$keyword" | tr '[:upper:]' '[:lower:]')"
    if [[ "$line_lower" == *"$keyword_lower"* ]]; then
      return 0
    fi
  done
  return 1
}

SCAN_FILES=()
while IFS= read -r -d '' file; do
  SCAN_FILES+=("$file")
done < <(find . -maxdepth 1 -type f -name 'README*.md' -print0)

while IFS= read -r -d '' file; do
  SCAN_FILES+=("$file")
done < <(find guide -type f -name '*.md' -print0)

for file in "${SCAN_FILES[@]}"; do
  for token in "${RULES[@]}"; do
    while IFS=: read -r line_no line_text; do
      [[ -z "${line_no:-}" ]] && continue
      relative="${file#./}"
      if is_allowed_context "$line_text"; then
        doc_warn "$relative:$line_no matched '$token' but exempted by example/reference context"
      else
        doc_error "$relative:$line_no matched '$token' and may be stale assertion"
      fi
    done < <(grep -nF "$token" "$file" || true)
  done
done

PATHS_TO_CHECK=("${REQUIRED_PATHS[@]}")
if [[ ${#EXTRA_PATHS[@]} -gt 0 ]]; then
  PATHS_TO_CHECK+=("${EXTRA_PATHS[@]}")
fi

for path in "${PATHS_TO_CHECK[@]}"; do
  if [[ ! -e "$path" ]]; then
    doc_error "missing path: $path"
  fi
done

if ! find guide/user -maxdepth 1 -type f -iname '*github-pages.md' | grep -q .; then
  doc_error "missing pages deployment guide under guide/user/*GitHub-Pages.md"
fi

if [[ "$ERROR_COUNT" -gt 0 ]]; then
  echo "ERROR: doc-asset consistency check failed, errors=$ERROR_COUNT warnings=$WARN_COUNT"
  exit 1
fi

echo "OK doc-asset consistency check passed, errors=0 warnings=$WARN_COUNT"
