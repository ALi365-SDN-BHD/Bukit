#!/usr/bin/env python3
path = 'src/skills/bukit-design-tokens/SKILL.md'
with open(path) as f:
    lines = f.readlines()

new_lines = []
for i, line in enumerate(lines):
    new_lines.append(line)
    if 'fonts.googleapis.com/css2?family=Inter' in line:
        new_lines.append('      - "https://cdn.jsdelivr.net/npm/modern-normalize/modern-normalize.min.css"\n')
        new_lines.append('    primary_color: "#7c3aed"\n')
        new_lines.append('    font_family: "Inter, system-ui, sans-serif"\n')
        new_lines.append('```\n')
        new_lines.append('\n')
        new_lines.append('For layout (grid, flex, spacing) use Tailwind. For theming (colors, fonts, content styles) use Bukit tokens.\n')
        new_lines.append('\n')
        new_lines.append('```html\n')

with open(path, 'w') as f:
    f.writelines(new_lines)
print("Fixed: YAML block restored")
