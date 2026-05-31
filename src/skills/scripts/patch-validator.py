#!/usr/bin/env python3
"""Add semantic check calls to validate-skills-strict.sh before Summary"""
path = 'src/skills/scripts/validate-skills-strict.sh'
with open(path) as f:
    lines = f.readlines()

# Find "# --- Summary ---" and insert before it
new_lines = []
for line in lines:
    if line.strip() == '# --- Summary ---':
        # Insert new checks
        new_lines.append('\n')
        new_lines.append('# --- Check 11: Markdown table consistency (no merged rows) ---\n')
        new_lines.append('echo ""\n')
        new_lines.append('echo "--- Check 11: Markdown table consistency ---"\n')
        new_lines.append('python3 "$SKILLS_DIR/scripts/check-markdown-tables.py" || ERRORS=$((ERRORS + 1))\n')
        new_lines.append('\n')
        new_lines.append('# --- Check 12: CLI commands consistency ---\n')
        new_lines.append('echo ""\n')
        new_lines.append('echo "--- Check 12: CLI commands consistency ---"\n')
        new_lines.append('python3 "$SKILLS_DIR/scripts/check-cli-commands.py" || true\n')
        new_lines.append('\n')
        new_lines.append('# --- Check 13: Status consistency ---\n')
        new_lines.append('echo ""\n')
        new_lines.append('echo "--- Check 13: Status consistency ---"\n')
        new_lines.append('python3 "$SKILLS_DIR/scripts/check-status-consistency.py" || ERRORS=$((ERRORS + 1))\n')
        new_lines.append('\n')
    new_lines.append(line)

with open(path, 'w') as f:
    f.writelines(new_lines)
print("Done: semantic checks added")
