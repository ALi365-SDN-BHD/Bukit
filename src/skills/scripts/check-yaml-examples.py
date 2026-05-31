#!/usr/bin/env python3
"""Check YAML code blocks in SKILL.md files for parseability"""
import os, sys, re, glob

skills_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

try:
    import yaml
except ImportError:
    print('  Warning: PyYAML not installed, skipping YAML validation')
    sys.exit(0)

errors = 0
for skill_file in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    skill_name = os.path.basename(os.path.dirname(skill_file))
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
