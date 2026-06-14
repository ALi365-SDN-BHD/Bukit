#!/usr/bin/env python3
"""Check YAML code blocks in SKILL.md files for parseability"""
import os, sys, re, glob

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

try:
    import yaml
except ImportError:
    if os.environ.get('ALLOW_SKIP_YAML_VALIDATION', '') == '1':
        print('  Warning: PyYAML not installed, skipping YAML validation')
        sys.exit(0)
    print('  ERROR: PyYAML not installed — YAML validation cannot run. Install: pip3 install pyyaml', file=sys.stderr)
    sys.exit(1)

errors = 0
all_files = sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md')))
extra_patterns = ['README.md', 'QUALITY_REPORT.md', 'MAINTENANCE.md',
                  'AGENTS.md', 'CLAUDE.md', 'GEMINI.md', 'copilot-instructions.md']
for pat in extra_patterns:
    path = os.path.join(skills_dir, pat)
    if os.path.exists(path):
        all_files.append(path)

for skill_file in sorted(all_files):
    if skill_file.endswith('/SKILL.md'):
        skill_name = os.path.basename(os.path.dirname(skill_file))
    else:
        skill_name = 'docs/' + os.path.splitext(os.path.basename(skill_file))[0]
    with open(skill_file) as f:
        content = f.read()
    
    # Find ```yaml blocks
    blocks = re.finditer(r'```yaml\n(.*?)```', content, re.DOTALL)
    for i, m in enumerate(blocks):
        yaml_text = m.group(1)
        if not yaml_text.strip():
            continue
        try:
            list(yaml.safe_load_all(yaml_text))
        except yaml.YAMLError as e:
            lineno = content[:m.start()].count('\n') + 1
            print(f'  [{skill_name}:{lineno}] YAML parse error: {e}')
            errors += 1

if errors:
    print(f'  {errors} YAML parse error(s) found')
    sys.exit(1)
else:
    print('  All YAML code blocks parse successfully')
