#!/usr/bin/env python3
path = 'src/skills/bukit-cli-reference/SKILL.md'
with open(path) as f:
    lines = f.readlines()

new_lines = []
i = 0
while i < len(lines):
    line = lines[i]
    # Fix line containing clone + geo audit merge
    if 'clone` (beta)' in line and 'geo audit' in line:
        # Split into two separate rows
        parts = line.split(' || ')
        clone_part = parts[0].rstrip() + '\n'
        geo_part = '| `geo audit` | GEO audit on dist output | `--dir` |\n'
        new_lines.append(clone_part)
        new_lines.append(geo_part)
        i += 1
        continue
    # Fix line containing docs check + version merge
    if 'docs check` (beta)' in line and 'version`' in line:
        parts = line.split(' || ')
        docs_part = parts[0].rstrip() + '\n'
        version_part = '| `version` | Output version number | No parameters |\n'
        new_lines.append(docs_part)
        new_lines.append(version_part)
        i += 1
        continue
    new_lines.append(line)
    i += 1

with open(path, 'w') as f:
    f.writelines(new_lines)
print("Done: CLI table merge fixed")
