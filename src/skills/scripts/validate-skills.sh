#!/usr/bin/env bash
set -euo pipefail

SKILLS_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ERRORS=0

echo "=== Bukit Skills Validator ==="
echo "Skills directory: $SKILLS_DIR"
echo ""

check_file_exists() {
  local file="$1"
  if [ ! -f "$file" ]; then
    echo "  ❌ MISSING: $file"
    ERRORS=$((ERRORS + 1))
    return 1
  fi
  return 0
}

check_front_matter() {
  local file="$1"
  local name="$2"

  if ! head -20 "$file" | grep -q "^name:"; then
    echo "  ❌ [$name] Missing 'name' in Front Matter"
    ERRORS=$((ERRORS + 1))
  fi

  if ! head -20 "$file" | grep -q "^description:"; then
    echo "  ❌ [$name] Missing 'description' in Front Matter"
    ERRORS=$((ERRORS + 1))
  fi
}

check_description_starts_with_use_when() {
  local file="$1"
  local name="$2"

  if ! head -20 "$file" | grep -q "^description:.*Use when"; then
    echo "  ⚠️  [$name] 'description' should start with 'Use when...'"
  fi
}

check_multilingual_triggers() {
  local file="$1"
  local name="$2"

  if ! grep -q "Multilingual Triggers\|Pencetus Berbilang Bahasa" "$file"; then
    echo "  ⚠️  [$name] Missing 'Multilingual Triggers' section"
  fi
}

check_common_errors() {
  local file="$1"
  local name="$2"

  if ! grep -qE "Common Error|Common Issue|Symptom.*Cause.*Fix" "$file"; then
    echo "  ⚠️  [$name] Missing 'Common Errors' section"
  fi
}

check_no_hardcoded_tool_names() {
  local file="$1"
  local name="$2"

  if grep -q "use Bash tool\|use the Bash tool\|用 Bash 工具执行" "$file"; then
    echo "  ❌ [$name] Contains hardcoded tool name ('Bash tool') — use platform-independent language"
    ERRORS=$((ERRORS + 1))
  fi
}

# Validate plugin.json paths (paths are relative to plugin.json location = $SKILLS_DIR)
echo "--- Validating plugin.json ---"
if check_file_exists "$SKILLS_DIR/plugin.json"; then
  passed=0
  failed=0
  for skill_path in $(python3 -c "
import json, sys
try:
    with open('$SKILLS_DIR/plugin.json') as f:
        data = json.load(f)
    for s in data.get('skills', []):
        print(s)
except Exception as e:
    print(f'ERROR: {e}', file=sys.stderr)
    sys.exit(1)
" 2>/dev/null); do
    if [ -f "$SKILLS_DIR/$skill_path" ]; then
      passed=$((passed + 1))
    else
      echo "  ❌ plugin.json path not found: $SKILLS_DIR/$skill_path"
      failed=$((failed + 1))
      ERRORS=$((ERRORS + 1))
    fi
  done
  echo "  ✅ plugin.json: $passed valid, $failed invalid"
fi

# List all skill directories
echo ""
echo "--- Validating SKILL.md files ---"
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")

  # Skip scripts/ and non-skill dirs
  case "$skill_name" in
    scripts) continue ;;
    *) ;;
  esac

  skill_file="$skill_dir/SKILL.md"

  if check_file_exists "$skill_file"; then
    echo "  ✅ $skill_name"
    check_front_matter "$skill_file" "$skill_name"
    check_description_starts_with_use_when "$skill_file" "$skill_name"
    check_multilingual_triggers "$skill_file" "$skill_name"
    check_common_errors "$skill_file" "$skill_name"
    check_no_hardcoded_tool_names "$skill_file" "$skill_name"
  fi
done

echo ""
echo "--- Validating skills-index.yaml ---"
if check_file_exists "$SKILLS_DIR/skills-index.yaml"; then
  skill_count=$(grep -cE "^\s+- name:" "$SKILLS_DIR/skills-index.yaml" || echo "0")
  echo "  Skills indexed: $skill_count"

  # Check that indexed skills have valid paths
  for skill_name in $(grep -E "^\s+- name:" "$SKILLS_DIR/skills-index.yaml" | sed 's/.*name: //'); do
    if [ ! -f "$SKILLS_DIR/$skill_name/SKILL.md" ]; then
      echo "  ❌ Indexed skill '$skill_name' has no SKILL.md"
      ERRORS=$((ERRORS + 1))
    fi
  done
fi

echo ""
if [ $ERRORS -eq 0 ]; then
  echo "=== ✅ All validations passed ==="
  exit 0
else
  echo "=== ❌ $ERRORS error(s) found ==="
  exit 1
fi
