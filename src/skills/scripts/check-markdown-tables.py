#!/usr/bin/env python3
"""Check for markdown table issues: merged rows and column count mismatches"""
import sys, os, re, glob

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
errors = 0

for skill_file in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    skill_name = os.path.basename(os.path.dirname(skill_file))
    with open(skill_file) as f:
        lines = f.readlines()
    for lineno, line in enumerate(lines, 1):
        stripped = line.strip()
        if stripped.startswith('|') and stripped.endswith('|'):
            # Remove inline code blocks to avoid false positives
            clean = re.sub(r'`[^`]*`', 'COL', stripped)
            if '||' in clean:
                print(f'  ERROR: [{skill_name}:{lineno}] Merged table row: {stripped[:120]}')
                errors += 1

if errors:
    print(f'  {errors} table issue(s) found')
    sys.exit(1)
else:
    print('  All Markdown tables clean')
