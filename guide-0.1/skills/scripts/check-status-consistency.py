#!/usr/bin/env python3
"""Check that SKILL.md status matches skills-index.yaml status"""
import os, sys, yaml

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Read index
index_path = os.path.join(skills_dir, 'skills-index.yaml')
with open(index_path) as f:
    idx = yaml.safe_load(f)

errors = 0
for skill in idx.get('skills', []):
    name = skill['name']
    yaml_status = skill.get('status', 'unknown')
    skill_path = os.path.join(skills_dir, name, 'SKILL.md')
    if not os.path.exists(skill_path):
        continue
    md_status = None
    with open(skill_path) as f:
        for line in f:
            if line.startswith('status:'):
                md_status = line.split(':', 1)[1].strip()
                break
    if md_status and md_status != yaml_status:
        print(f'  MISMATCH: {name} — SKILL.md: {md_status}, skills-index.yaml: {yaml_status}')
        errors += 1

if errors:
    print(f'  {errors} status mismatch(es) found')
    sys.exit(1)
else:
    print('  All status values consistent')
