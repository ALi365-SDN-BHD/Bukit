#!/usr/bin/env bash
set -euo pipefail

SKILLS_DIR="$(cd "$(dirname "$0")/.." && pwd)"
REPO_ROOT="$(cd "$SKILLS_DIR/../.." && pwd)"
export SKILLS_DIR
export REPO_ROOT
ERRORS=0
WARNINGS=0

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m'

echo "=== Bukit Skills Strict Validator ==="
echo "Skills directory: $SKILLS_DIR"
echo "Repo root: $REPO_ROOT"
echo ""

# --- 1. skill_count in skills-index.yaml matches actual skill count ---
echo "--- Check 1: skill_count consistency ---"
INDEX_YAML="$SKILLS_DIR/skills-index.yaml"
INDEX_JSON="$SKILLS_DIR/skills-index.json"
PLUGIN_JSON="$SKILLS_DIR/plugin.json"

declare_count=$(grep -cE "^\s+- name:" "$INDEX_YAML" || echo "0")
echo "  skills-index.yaml declares: $declare_count skills"

# Count actual SKILL.md files
actual_count=0
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  if [ -f "$skill_dir/SKILL.md" ]; then
    actual_count=$((actual_count + 1))
  fi
done
echo "  Actual SKILL.md files: $actual_count"

yaml_count=$(grep "^skill_count:" "$INDEX_YAML" | sed 's/skill_count: //')
echo "  skills-index.yaml skill_count field: $yaml_count"

if [ "$declare_count" -ne "$actual_count" ]; then
  echo -e "  ${RED}❌ skills-index.yaml declares $declare_count but $actual_count SKILL.md files exist${NC}"
  ERRORS=$((ERRORS + 1))
fi
if [ "$yaml_count" -ne "$actual_count" ]; then
  echo -e "  ${RED}❌ skill_count field ($yaml_count) != actual ($actual_count)${NC}"
  ERRORS=$((ERRORS + 1))
fi

# --- 2. plugin.json skills match skills-index.yaml ---
echo ""
echo "--- Check 2: plugin.json vs skills-index.yaml ---"
plugin_skills=$(python3 -c "
import json, sys, yaml
try:
    with open('$PLUGIN_JSON') as f:
        pj = json.load(f)
    with open('$INDEX_YAML') as f:
        idx = yaml.safe_load(f)
    errors = 0
    pset = set(s.replace('/SKILL.md','') for s in pj.get('skills',[]))
    iset = set(s['name'] for s in idx.get('skills',[]))
    missing_in_plugin = iset - pset
    extra_in_plugin = pset - iset
    if missing_in_plugin:
        print('MISSING_IN_PLUGIN:' + ','.join(sorted(missing_in_plugin)))
        errors += 1
    if extra_in_plugin:
        print('EXTRA_IN_PLUGIN:' + ','.join(sorted(extra_in_plugin)))
        errors += 1
    # Path match
    idx_paths = {s['name']: s['path'].replace('src/skills/', '') for s in idx.get('skills',[])}
    for ps in pj.get('skills',[]):
        name = ps.replace('/SKILL.md','')
        expected = idx_paths.get(name, '')
        if expected and ps != expected:
            print(f'PATH_MISMATCH: plugin.json has {ps}, index has {expected}')
            errors += 1
    # Order match
    idx_order = [s['name'] for s in idx.get('skills',[])]
    pj_order = [s.replace('/SKILL.md','') for s in pj.get('skills',[])]
    if idx_order != pj_order:
        print(f'ORDER_MISMATCH: plugin.json order differs from skills-index.yaml')
        print(f'  plugin.json order: {pj_order}')
        print(f'  index order:       {idx_order}')
        errors += 1
    # Skills count check
    if len(pj.get('skills',[])) != idx.get('skill_count', 0):
        print(f'COUNT_MISMATCH: plugin.json has {len(pj.get("skills",[]))} skills, index says {idx.get("skill_count", 0)}')
    # Version match
    if pj.get('version') != idx.get('version'):
        print(f'VERSION_MISMATCH: plugin.json={pj.get(\"version\")} vs index={idx.get(\"version\")}')
        errors += 1
    if not missing_in_plugin and not extra_in_plugin and errors == 0:
        print('MATCH')
    elif errors:
        sys.exit(1)
except Exception as e:
    print(f'ERROR:{e}', file=sys.stderr)
    sys.exit(1)
" 2>/dev/null)
if echo "$plugin_skills" | grep -q "MATCH"; then
  echo -e "  ${GREEN}✅ plugin.json and skills-index.yaml are consistent${NC}"
elif echo "$plugin_skills" | grep -q "MISSING_IN_PLUGIN"; then
  echo -e "  ${RED}❌ Missing from plugin.json: $(echo "$plugin_skills" | grep MISSING_IN_PLUGIN | sed 's/MISSING_IN_PLUGIN://')${NC}"
  ERRORS=$((ERRORS + 1))
else
  echo -e "  ${RED}❌$plugin_skills${NC}"
  ERRORS=$((ERRORS + 1))
fi

# --- 3. All SKILL.md have required Front Matter ---
echo ""
echo "--- Check 3: Front Matter completeness ---"
REQUIRED_FIELDS=("name:" "description:" "status:" "since:" "verified_by:" "source_anchors:" "guide_chapters:")
VALID_STATUSES="stable|beta|experimental|planned"

for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue

  for field in "${REQUIRED_FIELDS[@]}"; do
    if ! head -30 "$skill_file" | grep -q "^${field}"; then
      echo -e "  ${RED}❌ [$skill_name] Missing '$field' in Front Matter${NC}"
      ERRORS=$((ERRORS + 1))
    fi
  done

  # Check status value
  status_val=$(head -30 "$skill_file" | grep "^status:" | sed 's/status: //')
  if [ -n "$status_val" ]; then
    if ! echo "$status_val" | grep -qE "^($VALID_STATUSES)$"; then
      echo -e "  ${RED}❌ [$skill_name] Invalid status '$status_val' — must be one of: $VALID_STATUSES${NC}"
      ERRORS=$((ERRORS + 1))
    fi
  fi
done

# --- 4. source_anchors paths exist ---
echo ""
echo "--- Check 4: source_anchors paths ---"
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue

  # Extract source_anchors values
  in_anchors=0
  while IFS= read -r line; do
    if echo "$line" | grep -q "^source_anchors:"; then
      in_anchors=1
      continue
    fi
    if [ "$in_anchors" -eq 1 ]; then
      if echo "$line" | grep -qE "^\s+-"; then
        path_val=$(echo "$line" | sed 's/.*"\(.*\)".*/\1/')
        if [ ! -e "$REPO_ROOT/$path_val" ]; then
          echo -e "  ${YELLOW}⚠️  [$skill_name] source_anchors path not found: $path_val${NC}"
          WARNINGS=$((WARNINGS + 1))
        fi
      else
        in_anchors=0
      fi
    fi
  done < <(head -30 "$skill_file")
done

# --- 5. guide_chapters paths exist ---
echo ""
echo "--- Check 5: guide_chapters paths ---"
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue

  in_guides=0
  while IFS= read -r line; do
    if echo "$line" | grep -q "^guide_chapters:"; then
      in_guides=1
      continue
    fi
    if [ "$in_guides" -eq 1 ]; then
      if echo "$line" | grep -qE "^\s+-"; then
        path_val=$(echo "$line" | sed 's/.*"\(.*\)".*/\1/')
        if [ ! -f "$REPO_ROOT/$path_val" ]; then
          echo -e "  ${YELLOW}⚠️  [$skill_name] guide_chapters file not found: $path_val${NC}"
          WARNINGS=$((WARNINGS + 1))
        fi
      else
        in_guides=0
      fi
    fi
  done < <(head -30 "$skill_file")
done

# --- 6. No file:// or local absolute paths ---
echo ""
echo "--- Check 6: No local absolute paths ---"
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue

  if grep -q "file:///" "$skill_file" 2>/dev/null; then
    echo -e "  ${RED}❌ [$skill_name] Contains file:// URI — use relative paths${NC}"
    ERRORS=$((ERRORS + 1))
  fi
  if grep -qE "/Users/|/home/" "$skill_file" 2>/dev/null; then
    echo -e "  ${RED}❌ [$skill_name] Contains local absolute path (/Users/ or /home/)${NC}"
    ERRORS=$((ERRORS + 1))
  fi
done

# --- 7. No hardcoded platform tool names ---
echo ""
echo "--- Check 7: No hardcoded platform tool names ---"
FORBIDDEN_TERMS=("Bash tool" "TodoWrite" "Use the Bash tool" "用 Bash 工具执行")
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue

  for term in "${FORBIDDEN_TERMS[@]}"; do
    if grep -q "$term" "$skill_file" 2>/dev/null; then
      echo -e "  ${RED}❌ [$skill_name] Contains forbidden term: '$term'${NC}"
      ERRORS=$((ERRORS + 1))
    fi
  done
done

# --- 8. skills-index.json sync check ---
echo ""
echo "--- Check 8: skills-index.json sync ---"
python3 -c "
import json, yaml, sys
try:
    with open('$INDEX_YAML') as f:
        ydata = yaml.safe_load(f)
    with open('$INDEX_JSON') as f:
        jdata = json.load(f)
    # Deep comparison via canonical JSON dump
    ycanon = json.dumps(ydata, sort_keys=True, ensure_ascii=False)
    jcanon = json.dumps(jdata, sort_keys=True, ensure_ascii=False)
    if ycanon != jcanon:
        print('DISCREPANCY: skills-index.json does not exactly match skills-index.yaml')
        ylines = ycanon.split('\n')
        jlines = jcanon.split('\n')
        maxl = max(len(ylines), len(jlines))
        for i in range(maxl):
            yl = ylines[i] if i < len(ylines) else '<missing>'
            jl = jlines[i] if i < len(jlines) else '<missing>'
            if yl != jl:
                print(f'  First diff at line {i+1}:')
                print(f'    YAML: {yl[:120]}')
                print(f'    JSON: {jl[:120]}')
                break
        sys.exit(1)
    print('MATCH')
except Exception as e:
    print(f'ERROR: {e}', file=sys.stderr)
    sys.exit(1)
" 2>/dev/null
result=$?
if [ $result -eq 0 ]; then
  echo -e "  ${GREEN}✅ skills-index.json is in sync with skills-index.yaml${NC}"
else
  echo -e "  ${YELLOW}⚠️  skills-index.json may be out of sync — run generate-index-json.sh${NC}"
  WARNINGS=$((WARNINGS + 1))
fi

# --- 9. All requires dependencies point to existing skills ---
echo ""
echo "--- Check 9: requires dependency validity ---"
python3 -c "
import yaml, os, sys
with open('$INDEX_YAML') as f:
    data = yaml.safe_load(f)
all_names = set(s['name'] for s in data.get('skills',[]))
errors = 0
for s in data.get('skills',[]):
    for dep in s.get('requires', []):
        if dep not in all_names:
            print(f'  ❌ {s[\"name\"]} requires non-existent {dep}')
            errors += 1
if errors:
    sys.exit(1)
print('ALL_VALID')
" 2>/dev/null
if [ $? -eq 0 ]; then
  echo -e "  ${GREEN}✅ All requires dependencies point to existing skills${NC}"
else
  echo -e "  ${RED}❌ Some requires dependencies are invalid${NC}"
  ERRORS=$((ERRORS + 1))
fi

# --- 10. All workflow chain skills exist ---
echo ""
echo "--- Check 10: workflow chain validity ---"
python3 -c "
import yaml, sys
with open('$INDEX_YAML') as f:
    data = yaml.safe_load(f)
all_names = set(s['name'] for s in data.get('skills',[]))
errors = 0
for wname, w in data.get('workflows', {}).items():
    for s in w.get('chain', []):
        if s not in all_names:
            print(f'  ❌ Workflow {wname} references non-existent {s}')
            errors += 1
if errors:
    sys.exit(1)
print('ALL_VALID')
" 2>/dev/null
if [ $? -eq 0 ]; then
  echo -e "  ${GREEN}✅ All workflow chains reference valid skills${NC}"
else
  echo -e "  ${RED}❌ Some workflow chains reference invalid skills${NC}"
  ERRORS=$((ERRORS + 1))
fi


# --- Check 11: Markdown table consistency (no merged rows) ---
echo ""
echo "--- Check 11: Markdown table consistency ---"
python3 "$SKILLS_DIR/scripts/check-markdown-tables.py" || ERRORS=$((ERRORS + 1))

# --- Check 12: CLI commands consistency ---
echo ""
echo "--- Check 12: CLI commands consistency ---"
python3 "$SKILLS_DIR/scripts/check-cli-commands.py" || ERRORS=$((ERRORS + 1))

# --- Check 13: Status consistency ---
echo ""
echo "--- Check 13: Status consistency ---"
python3 "$SKILLS_DIR/scripts/check-status-consistency.py" || ERRORS=$((ERRORS + 1))


# --- Check 14: YAML code block validation ---
echo ""
echo "--- Check 14: YAML code block validation ---"
python3 "$SKILLS_DIR/scripts/check-yaml-examples.py" || ERRORS=$((ERRORS + 1))

# --- Check 15: Status keyword consistency ---
echo ""
echo "--- Check 15: Status keyword consistency ---"
python3 "$SKILLS_DIR/scripts/check-status-keywords.py" || WARNINGS=$((WARNINGS + 1))


# --- Check 16: Duplicate entries in skills-index.yaml ---
echo ""
echo "--- Check 16: No duplicate entries in skills-index.yaml ---"
python3 -c "
import yaml, os, sys
skills_dir = os.environ.get('SKILLS_DIR', 'src/skills')
index_path = os.path.join(skills_dir, 'skills-index.yaml')
with open(index_path) as f:
    data = yaml.safe_load(f)
errors = 0
for s in data.get('skills', []):
    name = s.get('name', '?')
    for field in ('source_anchors', 'verified_by', 'guide_chapters'):
        vals = s.get(field, [])
        seen = set()
        dup = []
        for v in vals:
            if v in seen:
                dup.append(v)
            else:
                seen.add(v)
        if dup:
            print(f'  WARNING: [{name}] {field} has duplicates: {dup}')
            errors += 1
if errors:
    print(f'  {errors} duplicate entry issue(s) found')
    sys.exit(1)
else:
    print('  No duplicate entries in skills-index.yaml')
    sys.exit(0)
" 2>/dev/null
if [ $? -ne 0 ]; then
  echo -e "  ${YELLOW}⚠️  Duplicate entries found in skills-index.yaml${NC}"
  WARNINGS=$((WARNINGS + 1))
fi

# --- Check 16b: Duplicate entries in SKILL.md Front Matter ---
echo ""
echo "--- Check 16b: No duplicate entries in SKILL.md Front Matter ---"
python3 -c "
import os, glob, sys
skills_dir = os.environ.get('SKILLS_DIR', 'src/skills')
errors = 0
for sf in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    name = os.path.basename(os.path.dirname(sf))
    with open(sf) as f:
        lines = f.readlines()
    # Track duplicates per YAML key (source_anchors, verified_by, guide_chapters)
    in_key = None
    seen = {}
    for line in lines:
        ls = line.lstrip()
        if ls.startswith('source_anchors:') or ls.startswith('verified_by:') or ls.startswith('guide_chapters:'):
            in_key = ls.split(':')[0]
            seen[in_key] = set()
            continue
        if ls.startswith('  - ') and in_key:
            val = line.strip()
            if val in seen.get(in_key, set()):
                print(f'  WARNING: [{name}] {in_key} has duplicate: {val}')
                errors += 1
            else:
                seen[in_key].add(val)
        elif ls and not ls.startswith('  - ') and not ls.startswith(' '):
            in_key = None
if errors:
    print(f'  {errors} duplicate entry issue(s) found')
    sys.exit(1)
else:
    print('  No duplicate entries in Front Matter')
    sys.exit(0)
" 2>/dev/null
if [ $? -ne 0 ]; then
  echo -e "  ${YELLOW}⚠️  Duplicate entries found in SKILL.md Front Matter${NC}"
  WARNINGS=$((WARNINGS + 1))
fi

# --- Summary ---
echo ""
echo "============================================"
if [ $ERRORS -eq 0 ] && [ $WARNINGS -eq 0 ]; then
  echo -e "  ${GREEN}=== ✅ All strict validations passed ===${NC}"
elif [ $ERRORS -eq 0 ]; then
  echo -e "  ${YELLOW}=== ⚠️  $WARNINGS warning(s) — $ERRORS error(s) ===${NC}"
  exit 0
else
  echo -e "  ${RED}=== ❌ $ERRORS error(s), $WARNINGS warning(s) ===${NC}"
  exit 1
fi
