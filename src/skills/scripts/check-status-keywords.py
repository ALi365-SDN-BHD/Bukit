#!/usr/bin/env python3
"""Check that SKILL.md body phrases don't contradict Front Matter status.

This intentionally ignores casual mentions like "Experimental registry install"
inside otherwise stable skills, and only flags phrases that imply the skill
itself is planned/experimental or not implemented.
"""
import os, sys, glob, re

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

WARNING_PATTERNS = [
    r'\bthis skill is experimental\b',
    r'\bthis skill is planned\b',
    r'\bthis workflow is experimental\b',
    r'\bthis workflow is planned\b',
    r'\bstatus\s*[:=]\s*(experimental|planned)\b',
    r'\bnot yet implemented\b',
    r'\bnot implemented\b',
]

warnings = 0
for skill_file in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    skill_name = os.path.basename(os.path.dirname(skill_file))
    with open(skill_file) as f:
        content = f.read()
    
    # Read status
    parts = content.split('---', 2)
    if len(parts) < 3:
        continue
    fm = parts[1]
    md_status = 'unknown'
    for line in fm.split('\n'):
        if line.strip().startswith('status:'):
            md_status = line.split(':', 1)[1].strip()
            break
    
    # Check body content
    body = parts[2].lower()
    found_keywords = []
    for pattern in WARNING_PATTERNS:
        if re.search(pattern, body):
            found_keywords.append(pattern)
    
    if found_keywords and md_status == 'stable':
        print(f'  WARNING: [{skill_name}] status=stable but body contains: {found_keywords}')
        warnings += 1

if warnings:
    print(f'  {warnings} keyword/status mismatch(es) found')
    sys.exit(1)
else:
    print('  All keyword/status combinations consistent')
    sys.exit(0)
