#!/usr/bin/env python3
"""Fix P1-3: Add missing CLI params to bukit-cli-reference"""
path = 'src/skills/bukit-cli-reference/SKILL.md'
with open(path) as f:
    lines = f.readlines()

# 1. Fix preview quick-ref: add --config --site
for i, line in enumerate(lines):
    if 'preview` | Static preview of dist/' in line and '--strict-port' in line:
        lines[i] = line.replace('`--strict-port`', '`--strict-port` `--config` `--site`')
        print(f"  Fixed preview quick-ref at line {i+1}")
        break

# 2. Fix build quick-ref: add --allow-external-plugins
for i, line in enumerate(lines):
    if 'build` | Build static site' in line and '--log-format`' in line:
        lines[i] = line.replace('`--log-format`', '`--log-format` `--allow-external-plugins`')
        print(f"  Fixed build quick-ref at line {i+1}")
        break

# 3. Add --allow-external-plugins to build detailed params section
# Find the build params table and add row
for i, line in enumerate(lines):
    if line.strip() == '| `--log-format` | Log format: `text` (default) or `json` |':
        lines.insert(i + 1, '| `--allow-external-plugins` | Allow loading external protocol plugins (overrides `site.externalPluginPolicy`) |\n')
        print(f"  Added --allow-external-plugins to build params at line {i+2}")
        break

# 4. Add --config --site to preview detailed section
for i, line in enumerate(lines):
    if '| `--strict-port` | false | Error immediately on port conflict' in line:
        lines.insert(i + 1, '| `--config` | `site.yaml` | Config file path |\n')
        lines.insert(i + 2, '| `--site` | — | Multi-site name |\n')
        print(f"  Added --config --site to preview params at line {i+2}")
        break

with open(path, 'w') as f:
    f.writelines(lines)
print("Done: CLI params fixed")
