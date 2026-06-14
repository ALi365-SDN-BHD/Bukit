#!/usr/bin/env python3
"""Add semantic checks to validate-skills-strict.sh"""
path = 'src/skills/scripts/validate-skills-strict.sh'
with open(path) as f:
    content = f.read()

# Find the Summary section and add new checks before it
checks_block = '''

# --- Check 11: Markdown table consistency (no merged rows) ---
echo ""
echo "--- Check 11: Markdown table consistency ---"
for skill_dir in "$SKILLS_DIR"/*/; do
  skill_name=$(basename "$skill_dir")
  case "$skill_name" in scripts) continue ;; esac
  skill_file="$skill_dir/SKILL.md"
  [ ! -f "$skill_file" ] && continue
  # Check for double pipe in table rows (indicates merged rows)
  if grep -nE '^\|.*\|\|' "$skill_file" 2>/dev/null; then
    echo -e "  ${RED}❌ [$skill_name] Contains merged table rows ('||') — split into separate rows${NC}"
    ERRORS=$((ERRORS + 1))
  fi
  # Check table row consistency: rows within same table should have same column count
  python3 -c "
import sys
with open('$skill_file') as f:
    in_table = False
    col_count = 0
    for lineno, line in enumerate(f, 1):
        stripped = line.strip()
        if stripped.startswith('|') and stripped.endswith('|'):
            cols = len(stripped.split('|')) - 2
            if not in_table:
                in_table = True
                col_count = cols
            elif cols != col_count:
                print(f'{lineno}: expected {col_count} cols, got {cols}: {stripped[:80]}')
        elif in_table and not stripped.startswith('|'):
            in_table = False
            col_count = 0
" 2>/dev/null
  if [ $? -ne 0 ]; then
    echo -e "  ${YELLOW}⚠️  [$skill_name] Inconsistent table column counts${NC}"
    WARNINGS=$((WARNINGS + 1))
  fi
done

# --- Check 12: No duplicate commands in CLI reference ---
echo ""
echo "--- Check 12: Verify CLI commands exist in source ---"
python3 -c "
import re, os

# Extract commands from BukitCliSpecs.cs
specs_path = '$REPO_ROOT/src/Bukit.Cli/Cli/BukitCliSpecs.cs'
cli_commands = set()
if os.path.exists(specs_path):
    with open(specs_path) as f:
        text = f.read()
    # Find top-level command names
    for m in re.finditer(r'Name:\\s*\"([^\"]+)\"', text):
        cli_commands.add(m.group(1))
    # Find subcommand names (parent.child)
    # Simple approach: find Name: strings in subcommand contexts
    print(f'  Source has {len(cli_commands)} registered commands')

# Extract commands from CLI reference skill
ref_path = '$SKILLS_DIR/bukit-cli-reference/SKILL.md'
ref_commands = set()
if os.path.exists(ref_path):
    with open(ref_path) as f:
        for line in f:
            m = re.match(r'\\| `([^`]+)` \\|', line)
            if m:
                cmd = m.group(1).split(' (')[0]
                ref_commands.add(cmd)
    print(f'  CLI reference has {len(ref_commands)} documented commands')

# Cross-check
source_only = cli_commands - ref_commands
ref_only = ref_commands - cli_commands
if source_only:
    print(f'  Commands in source but NOT in CLI reference: {sorted(source_only)}')
if ref_only:
    print(f'  Commands in CLI reference but NOT in source: {sorted(ref_only)}')
" 2>/dev/null

# --- Check 13: SKILL.md status matches skills-index.yaml ---
echo ""
echo "--- Check 13: Status consistency between SKILL.md and skills-index.yaml ---"
python3 -c "
import yaml, os, sys
with open('$SKILLS_DIR/skills-index.yaml') as f:
    idx = yaml.safe_load(f)
errors = 0
for skill in idx.get('skills', []):
    name = skill['name']
    yaml_status = skill.get('status', 'unknown')
    skill_path = os.path.join('$SKILLS_DIR', name, 'SKILL.md')
    if not os.path.exists(skill_path):
        continue
    with open(skill_path) as f:
        for line in f:
            if line.startswith('status:'):
                md_status = line.split(':', 1)[1].strip()
                if md_status != yaml_status:
                    print(f'  MISMATCH: {name} — SKILL.md: {md_status}, index: {yaml_status}')
                    errors += 1
                break
if errors:
    sys.exit(1)
print('ALL_CONSISTENT')
" 2>/dev/null
if [ $? -eq 0 ]; then
  echo -e "  ${GREEN}✅ All status values consistent between SKILL.md and skills-index.yaml${NC}"
else
  echo -e "  ${RED}❌ Status mismatch found${NC}"
  ERRORS=$((ERRORS + 1))
fi

'''

# Insert before Summary
content = content.replace('\n# --- Summary ---', checks_block + '\n\n# --- Summary ---')

with open(path, 'w') as f:
    f.write(content)
print("Done: semantic checks added to validate-skills-strict.sh")
