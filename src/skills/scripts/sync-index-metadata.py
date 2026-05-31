#!/usr/bin/env python3
"""Sync status/source_anchors/since/verified_by from SKILL.md to skills-index.yaml (line-based)"""
import os

SKILLS_DIR = "src/skills"

def read_front_matter(path):
    with open(path) as f:
        content = f.read()
    parts = content.split('---', 2)
    if len(parts) < 3:
        return {}
    fm = {}
    current_key = None
    for line in parts[1].strip().split('\n'):
        stripped = line.strip()
        if not stripped:
            continue
        if stripped.startswith('- '):
            if current_key and current_key in fm:
                fm[current_key].append(stripped[2:].strip().strip('"'))
        elif ':' in stripped:
            key, val = stripped.split(':', 1)
            key = key.strip()
            val = val.strip().strip('"')
            if val == '':
                fm[key] = []
                current_key = key
            else:
                fm[key] = val
                current_key = None
    return fm

def format_list(indent, items):
    return '\n'.join(f'{indent}- "{item}"' for item in items)

# Read all front matter data
fm_data = {}
for entry in sorted(os.listdir(SKILLS_DIR)):
    skill_file = os.path.join(SKILLS_DIR, entry, "SKILL.md")
    if not os.path.isfile(skill_file):
        continue
    fm_data[entry] = read_front_matter(skill_file)

# Read skills-index.yaml
index_path = os.path.join(SKILLS_DIR, "skills-index.yaml")
with open(index_path) as f:
    lines = f.readlines()

# Process: find each skill entry and insert metadata before its 'guide_chapter:' line
new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    new_lines.append(line)
    
    # Detect start of a skill entry by looking for '- name: ' at proper indent
    if line.startswith('  - name: '):
        skill_name = line.split('name: ')[1].strip()
        if skill_name in fm_data:
            fm = fm_data[skill_name]
            
            # Collect metadata to insert before guide_chapter
            # We need to find the guide_chapter line for this skill and insert before it
            # First, let's look ahead to find guide_chapter
            metadata_lines = []
            if 'status' in fm:
                metadata_lines.append(f'    status: {fm["status"]}\n')
            if 'since' in fm:
                metadata_lines.append(f'    since: {fm["since"]}\n')
            
            src_anchors = fm.get('source_anchors', [])
            if src_anchors:
                metadata_lines.append('    source_anchors:\n')
                for a in src_anchors:
                    metadata_lines.append(f'      - "{a}"\n')
            
            verified = fm.get('verified_by', [])
            if verified:
                metadata_lines.append('    verified_by:\n')
                for v in verified:
                    metadata_lines.append(f'      - "{v}"\n')
            
            guides = fm.get('guide_chapters', [])
            if guides:
                metadata_lines.append('    guide_chapters:\n')
                for g in guides:
                    metadata_lines.append(f'      - "{g}"\n')
            
            # Now find the guide_chapter line (or platform_loading line) and insert before it
            # Look ahead for the next skill entry or end of file
            insert_pos = None
            for j in range(i + 1, len(lines)):
                if lines[j].strip().startswith('guide_chapter:') or \
                   lines[j].strip().startswith('platform_loading:') or \
                   lines[j].strip().startswith('user_invocable:'):
                    insert_pos = j
                    break
                elif lines[j].startswith('  - name: '):  # next skill
                    insert_pos = j
                    break
                elif lines[j].startswith('# ===') or lines[j].startswith('workflows:'):
                    insert_pos = j
                    break
            
            if insert_pos:
                for k, meta_line in enumerate(metadata_lines):
                    new_lines.append(meta_line)
    i += 1

with open(index_path, 'w') as f:
    f.writelines(new_lines)
print(f"Done: skills-index.yaml updated with metadata from {len(fm_data)} skills")
