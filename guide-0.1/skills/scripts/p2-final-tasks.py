#!/usr/bin/env python3
"""P2: Add Beta notice to README + dedup source_anchors"""
import os

skills_dir = 'src/skills'

# P2.2: source_anchors dedup
for entry in sorted(os.listdir(skills_dir)):
    sf = os.path.join(skills_dir, entry, 'SKILL.md')
    if not os.path.isfile(sf):
        continue
    with open(sf) as f:
        content = f.read()
    lines = content.split('\n')
    seen_src = set()
    seen_ver = set()
    new = []
    in_src = False
    in_ver = False
    for l in lines:
        if l.strip() == 'source_anchors:':
            in_src = True
            new.append(l)
            continue
        if l.strip() == 'verified_by:':
            in_ver = True
            new.append(l)
            continue
        if in_src and l.startswith('  - '):
            if l in seen_src:
                continue
            seen_src.add(l)
            new.append(l)
            continue
        if in_ver and l.startswith('  - '):
            if l in seen_ver:
                continue
            seen_ver.add(l)
            new.append(l)
            continue
        if in_src and not l.startswith('  - '):
            in_src = False
        if in_ver and not l.startswith('  - '):
            in_ver = False
        new.append(l)
    with open(sf, 'w') as f:
        f.write('\n'.join(new))
print('P2.2: source_anchors dedup done')

# P2.3: Beta notice
path = os.path.join(skills_dir, 'README.md')
with open(path) as f:
    c = f.read()
notice = '> **Status: Beta** — These skills are actively maintained and verified against source code,\n> but the knowledge base structure and validation tooling may evolve. See [QUALITY_REPORT.md](QUALITY_REPORT.md)\n> for known issues.\n\n'
c = notice + c
with open(path, 'w') as f:
    f.write(c)
print('P2.3: README Beta status added')
