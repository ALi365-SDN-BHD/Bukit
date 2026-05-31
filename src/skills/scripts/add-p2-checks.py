#!/usr/bin/env python3
path = 'src/skills/scripts/validate-skills-strict.sh'
with open(path) as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if line.strip() == '# --- Summary ---':
        new_lines.append('\n')
        new_lines.append('# --- Check 14: YAML code block validation ---\n')
        new_lines.append('echo ""\n')
        new_lines.append('echo "--- Check 14: YAML code block validation ---"\n')
        new_lines.append('python3 "$SKILLS_DIR/scripts/check-yaml-examples.py" || true\n')
        new_lines.append('\n')
        new_lines.append('# --- Check 15: Status keyword consistency ---\n')
        new_lines.append('echo ""\n')
        new_lines.append('echo "--- Check 15: Status keyword consistency ---"\n')
        new_lines.append('python3 "$SKILLS_DIR/scripts/check-status-keywords.py" || true\n')
        new_lines.append('\n')
    new_lines.append(line)

with open(path, 'w') as f:
    f.writelines(new_lines)
print("Done")
