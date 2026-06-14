#!/usr/bin/env python3
path = 'scripts/quality-gate.sh'
with open(path) as f:
    lines = f.readlines()

# Find encoding check and insert after it
insert_at = None
for i, line in enumerate(lines):
    if 'bash scripts/check-encoding.sh' in line:
        insert_at = i + 1
        break

if insert_at is None:
    print("ERROR: Could not find encoding check")
    exit(1)

block = [
    '\n',
    '# --- Skills strict validation ---\n',
    'bash src/skills/scripts/validate-skills-strict.sh || { echo "ERROR: Skills strict validation failed"; exit 1; }\n',
    '\n',
]

for i, line in enumerate(block):
    lines.insert(insert_at + i, line)

with open(path, 'w') as f:
    f.writelines(lines)
print("Done: CI step added to quality-gate.sh")
