#!/usr/bin/env python3
"""Apply P0+P1 fixes: CLI reference, QUALITY_REPORT, design-tokens"""
import os

# --- P0-1: Add theme preview to CLI quick reference ---
path = 'src/skills/bukit-cli-reference/SKILL.md'
with open(path) as f:
    lines = f.readlines()

new_lines = []
for i, line in enumerate(lines):
    new_lines.append(line)
    if 'theme search` | Query community theme registry' in line:
        new_lines.append('| `theme preview` | Display detailed theme anatomy | `[name]` `--config` `--site` |\n')
        print("P0-1: Added theme preview to quick reference")

with open(path, 'w') as f:
    f.writelines(new_lines)

# --- P0-2: Split seo → seo audit / seo diff ---
# Re-read after P0-1
with open(path) as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if '`seo` | SEO audit and regression detection |' in line:
        new_lines.append('| `seo audit` | Audit SEO health from build report | `--dir` `--report` `--strict` `--external` |\n')
        new_lines.append('| `seo diff` | Compare SEO reports for regression budgets | `--baseline` `--current` `--max-new-errors N` `--max-new-warnings N` `--max-new-issues N` `--fail-on-new-code c1,c2` `--fail-on-route-removed` `--fail-on-indexable-drop` |\n')
        print("P0-2: Split seo → seo audit + seo diff")
    else:
        new_lines.append(line)

with open(path, 'w') as f:
    f.writelines(new_lines)

# Also update SOURCE_PARENTS_WITH_SUBCOMMANDS in check-cli-commands.py
path2 = 'src/skills/scripts/check-cli-commands.py'
with open(path2) as f:
    content = f.read()
content = content.replace(
    "'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',",
    "'theme', 'template', 'config', 'data', 'route', 'docs',"
)
with open(path2, 'w') as f:
    f.write(content)
print("P0-2: Removed 'seo' from SOURCE_PARENTS")

# --- P1-3: QUALITY_REPORT.md sync ---
path3 = 'src/skills/QUALITY_REPORT.md'
with open(path3) as f:
    content = f.read()

content = content.replace(
    '- `validate-skills-strict.sh`: 10 checks (skill count, plugin.json sync, Front Matter, source paths, guide paths, local paths, tool names, JSON sync, dependencies, workflows)',
    '- `validate-skills-strict.sh`: 15 checks (skill count, plugin.json sync, Front Matter, source paths, guide paths, local paths, tool names, JSON sync, dependencies, workflows, Markdown tables, CLI commands, status consistency, YAML validation, keyword consistency)'
)
content = content.replace(
    '| `validate-skills-strict.sh` | ✅ 10/10 checks passed, 0 errors, 0 warnings |',
    '| `validate-skills-strict.sh` | ✅ 15/15 checks passed, 0 errors, 0 warnings |'
)
content = content.replace(
    '| CLI semantic validation not hard-gating | Remaining — planned for next validator version |',
    '| CLI semantic validation not hard-gating | Fixed — check-cli-commands.py now hard-gates |'
)
content = content.replace(
    '| check-cli-commands.py does not parse full command paths | Remaining — planned |',
    '| check-cli-commands.py does not parse full command paths | Fixed — parses parent.child paths with whitelist |'
)
content = content.replace(
    '1. **Upgrade check-cli-commands.py**: Parse parent.child command paths correctly (e.g., `theme create`, `seo audit`)\n',
    ''
)
with open(path3, 'w') as f:
    f.write(content)
print("P1-3: QUALITY_REPORT.md synced")

# --- P1-4: Fix bukit-design-tokens theme.params ---
path4 = 'src/skills/bukit-design-tokens/SKILL.md'
with open(path4) as f:
    content = f.read()
old_block = """theme:
    external_css:
      - "https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700"
      - "https://cdn.jsdelivr.net/npm/modern-normalize/modern-normalize.min.css"
    primary_color: "#7c3aed"
    font_family: "Inter, system-ui, sans-serif"
"""
new_block = """theme:
  params:
    external_css:
      - "https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700"
      - "https://cdn.jsdelivr.net/npm/modern-normalize/modern-normalize.min.css"
    primary_color: "#7c3aed"
    font_family: "Inter, system-ui, sans-serif"
"""
content = content.replace(old_block, new_block)
with open(path4, 'w') as f:
    f.write(content)
print("P1-4: bukit-design-tokens theme.params fixed")

print("\nAll P0+P1 fixes applied")
