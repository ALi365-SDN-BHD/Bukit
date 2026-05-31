#!/usr/bin/env python3
"""Check for markdown table issues: merged rows, column counts, duplicate commands, table consistency"""
import sys, os, re, glob

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
errors = 0

for skill_file in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    skill_name = os.path.basename(os.path.dirname(skill_file))
    with open(skill_file) as f:
        lines = f.readlines()

    # Track tables: find table blocks and check consistency
    in_table = False
    header_cols = 0
    table_start = 0
    seen_cmds = {}  # command → line number (for duplicate detection)

    for lineno, line in enumerate(lines, 1):
        stripped = line.strip()

        if not stripped.startswith('|'):
            if in_table:
                in_table = False
                header_cols = 0
            continue

        if not stripped.endswith('|'):
            continue

        # Remove inline code to avoid false positives
        clean = re.sub(r'`[^`]*`', 'COL', stripped)
        clean = re.sub(r'\[([^\]]+)\]\([^\)]+\)', r'\1', clean)

        # Check for merged rows
        if '||' in clean:
            print(f'  ERROR: [{skill_name}:{lineno}] Merged table row (||)')
            errors += 1

        # Count columns
        cols = len(clean.split('|')) - 2  # leading and trailing |

        if not in_table:
            # New table block
            in_table = True
            header_cols = cols
            table_start = lineno
            # Check for separator row (should be next)
            if lineno + 1 <= len(lines):
                next_line = lines[lineno].strip()  # 0-indexed
                if re.match(r'^\|[\s\-|:]+\|$', next_line):
                    continue  # separator row is fine
            continue

        # Skip separator rows
        if re.match(r'^[\|\s\-:]+$', clean.replace('COL', '')):
            continue

        if cols != header_cols:
            print(f'  WARNING: [{skill_name}:{lineno}] Column count mismatch: expected {header_cols} cols, got {cols}: {stripped[:100]}')
            errors += 1

        # Check for duplicate commands in Quick Reference tables
        if skill_name == 'bukit-cli-reference' and 'Command | Purpose | Key Parameters' in lines[table_start-1]:
            m = re.match(r'\| `([^`]+)` \|', stripped)
            if m:
                cmd = m.group(1).strip()
                if cmd in seen_cmds:
                    print(f'  ERROR: [{skill_name}:{lineno}] Duplicate command: {cmd} (also at line {seen_cmds[cmd]})')
                    errors += 1
                seen_cmds[cmd] = lineno

if errors:
    print(f'  {errors} table issue(s) found')
    sys.exit(1)
else:
    print('  All Markdown tables clean')
