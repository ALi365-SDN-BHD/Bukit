#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

error_count=0
warn_count=0

rules=(
  "src/BukitJalil"
  "BukitJalil.slnx"
  "tools/ImageSharp"
  ".github/workflows/smoke.yml"
  ".github/workflows/build.yaml"
)

allow_keywords=(
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

required_paths=(
  "bukit.slnx"
  "guide/dev"
  "guide/user"
)

root_readmes=(
  "README.md"
  "README.zh-CN.md"
  "README.ms.md"
)

forbidden_root_readme_patterns=(
  "HMR"
  "Hot Module"
  "src/skills"
  "guide/ai/chatgpt"
  "examples/starter"
  ".github/workflows/release.yml"
)

forbidden_root_readme_commands=(
  "init"
  "create"
  "clone"
  "import"
  "theme"
  "plugin"
  "intent"
  "webhook"
  "notion"
  "visual"
  "data"
  "route"
)

extra_paths=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    --extra-path)
      shift
      if [ "$#" -eq 0 ]; then
        echo "ERROR: --extra-path requires a value" >&2
        exit 2
      fi
      extra_paths+=("$1")
      ;;
    *)
      echo "WARN: unknown argument '$1' ignored"
      ;;
  esac
  shift
done

doc_error() {
  error_count=$((error_count + 1))
  echo "ERROR: $1"
}

doc_warn() {
  warn_count=$((warn_count + 1))
  echo "WARN: $1"
}

is_allowed_context() {
  local line_lower keyword keyword_lower
  line_lower="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  for keyword in "${allow_keywords[@]}"; do
    keyword_lower="$(printf '%s' "$keyword" | tr '[:upper:]' '[:lower:]')"
    if [[ "$line_lower" == *"$keyword_lower"* ]]; then
      return 0
    fi
  done
  return 1
}

scan_files=()
while IFS= read -r -d '' file; do
  scan_files+=("$file")
done < <(find . -maxdepth 1 -type f -name 'README*.md' -print0)

while IFS= read -r -d '' file; do
  scan_files+=("$file")
done < <(find guide -type f -name '*.md' -print0)

for file in "${scan_files[@]}"; do
  for token in "${rules[@]}"; do
    while IFS=: read -r line_no line_text; do
      [ -z "${line_no:-}" ] && continue
      relative="${file#./}"
      if is_allowed_context "$line_text"; then
        doc_warn "$relative:$line_no matched '$token' but is in example/reference context"
      else
        doc_error "$relative:$line_no matched '$token' and may be stale"
      fi
    done < <(grep -nF "$token" "$file" || true)
  done
done

for file in "${root_readmes[@]}"; do
  if [ ! -f "$file" ]; then
    doc_error "missing root README: $file"
    continue
  fi

  for token in "${forbidden_root_readme_patterns[@]}"; do
    while IFS=: read -r line_no line_text; do
      [ -z "${line_no:-}" ] && continue
      doc_error "$file:$line_no contains root README forbidden token '$token'"
    done < <(grep -nF "$token" "$file" || true)
  done

  for command in "${forbidden_root_readme_commands[@]}"; do
    while IFS=: read -r line_no line_text; do
      [ -z "${line_no:-}" ] && continue
      doc_error "$file:$line_no uses non-Core command example 'bukit $command'"
    done < <(grep -nE "(^|[^[:alnum:]_-])bukit[[:space:]]+$command([^[:alnum:]_-]|$)" "$file" || true)
  done
done

paths_to_check=("${required_paths[@]}")
if [ "${#extra_paths[@]}" -gt 0 ]; then
  paths_to_check+=("${extra_paths[@]}")
fi

for path in "${paths_to_check[@]}"; do
  if [ ! -e "$path" ]; then
    doc_error "missing path: $path"
  fi
done

if ! find guide/user -maxdepth 1 -type f -iname '*github-pages.md' | grep -q .; then
  doc_error "missing pages deployment guide under guide/user/*github-pages.md"
fi

if [ "$error_count" -gt 0 ]; then
  echo "ERROR: doc consistency check failed, errors=$error_count warnings=$warn_count" >&2
  exit 1
fi

echo "Docs consistency check OK, warnings=$warn_count"
