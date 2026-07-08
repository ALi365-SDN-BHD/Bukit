#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

readmes=(README.md README.zh-CN.md README.ms.md)
core_commands=(build doctor config preview dev clean version completion seo geo publish deploy)
required_tokens=(
  "guide/user"
  "guide/dev"
  "guide/skills"
  "guide/labs"
  "guide/labs-skills"
  "github-pages"
  "bukit deploy"
  "LiveReload"
)
forbidden_patterns=(
  "bukit[[:space:]]+clone"
  "bukit[[:space:]]+import"
  "bukit[[:space:]]+theme"
  "bukit[[:space:]]+plugin"
  "HMR"
)
non_core_token_groups=(
  "clone"
  "import"
  "webhook"
  "theme registry|主题注册表|pendaftaran tema"
  "theme marketplace|主题市场|pasaran tema"
  "external plugin|外部插件|plugin luaran"
)

error_count=0

error() {
  error_count=$((error_count + 1))
  echo "ERROR: $1" >&2
}

extract_commands() {
  local file="$1"
  python3 - "$file" <<'PY'
import re
import sys
from pathlib import Path

text = Path(sys.argv[1]).read_text(encoding="utf-8")
for command in re.findall(r"\| `([a-z]+)` \|", text):
    print(command)
PY
}

for file in "${readmes[@]}"; do
  if [ ! -f "$file" ]; then
    error "missing README file: $file"
    continue
  fi

  actual_commands="$(extract_commands "$file" | sort | tr '\n' ' ')"
  expected_commands="$(printf "%s\n" "${core_commands[@]}" | sort | tr '\n' ' ')"
  if [ "$actual_commands" != "$expected_commands" ]; then
    error "$file Core command table mismatch: expected '$expected_commands' got '$actual_commands'"
  fi

  for token in "${required_tokens[@]}"; do
    if ! grep -Fq -- "$token" "$file"; then
      error "$file missing required synchronized token: $token"
    fi
  done

  for token_group in "${non_core_token_groups[@]}"; do
    IFS='|' read -r -a variants <<< "$token_group"
    found=0
    for token in "${variants[@]}"; do
      if grep -Fiq -- "$token" "$file"; then
        found=1
        break
      fi
    done

    if [ "$found" -eq 0 ]; then
      error "$file missing non-Core exclusion token group: $token_group"
    fi
  done

  for pattern in "${forbidden_patterns[@]}"; do
    while IFS=: read -r line_no line_text; do
      [ -z "${line_no:-}" ] && continue
      error "$file:$line_no contains forbidden README text matching '$pattern': $line_text"
    done < <(grep -nE "$pattern" "$file" || true)
  done
done

if [ "$error_count" -ne 0 ]; then
  echo "README sync check failed: errors=$error_count" >&2
  exit 1
fi

echo "README sync check OK"
