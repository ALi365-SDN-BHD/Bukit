#!/usr/bin/env python3
"""Apply all round 4 fixes"""
import os

# ==== P0: Add seo back to SOURCE_PARENTS ====
path = 'src/skills/scripts/check-cli-commands.py'
with open(path) as f:
    content = f.read()
content = content.replace(
    "'theme', 'template', 'config', 'data', 'route', 'docs',",
    "'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',"
)
with open(path, 'w') as f:
    f.write(content)
print("P0: seo added back to SOURCE_PARENTS")

# ==== P1-1: QUALITY_REPORT.md sync ====
path2 = 'src/skills/QUALITY_REPORT.md'
with open(path2) as f:
    c = f.read()
c = c.replace(
    '| check-cli-commands.py inline subcommand parsing | Remaining — inline Name: detection needs hardening |',
    '| check-cli-commands.py inline subcommand parsing | Fixed — inline Name: detection added; parser still regex-based |'
)
c = c.replace(
    '1. **Upgrade check-cli-commands.py**: Parse parent.child command paths correctly (e.g., `theme create`, `seo audit`)\n',
    ''
)
c = c.replace(
    '2. **Add Markdown table column-count consistency** to check-markdown-tables.py validator\n',
    ''
)
c = c.replace(
    '3. **Add YAML example parsing validation** to catch malformed YAML code blocks\n',
    ''
)
c = c.replace('4. **Run `dotnet test`', '1. **Run `dotnet test`')
c = c.replace('5. **Add `validate-skills-strict.sh`', '2. **Add `validate-skills-strict.sh`')
with open(path2, 'w') as f:
    f.write(c)
print("P1-1: QUALITY_REPORT.md synced")

# ==== P1-2: check-yaml-examples.py hard fail ====
path3 = 'src/skills/scripts/check-yaml-examples.py'
with open(path3) as f:
    c = f.read()
old = """except ImportError:
    print('  Warning: PyYAML not installed, skipping YAML validation')
    sys.exit(0)"""
new = """except ImportError:
    if os.environ.get('ALLOW_SKIP_YAML', '') == '1':
        print('  Warning: PyYAML not installed, skipping YAML validation')
        sys.exit(0)
    print('  ERROR: PyYAML not installed — YAML validation cannot run. Install: pip3 install pyyaml', file=sys.stderr)
    sys.exit(1)"""
c = c.replace(old, new)
with open(path3, 'w') as f:
    f.write(c)
print("P1-2: check-yaml-examples.py now hard-fails on missing PyYAML")

# ==== P1-3: generate-index-json.sh last else ====
path4 = 'src/skills/scripts/generate-index-json.sh'
with open(path4) as f:
    c = f.read()
old = """  echo "Warning: Neither yq nor python3 is available. Skipping JSON generation."
  exit 0
fi"""
new = """  echo "ERROR: Neither yq nor python3 is available — refusing to skip JSON generation" >&2
  exit 1
fi"""
c = c.replace(old, new)
with open(path4, 'w') as f:
    f.write(c)
print("P1-3: generate-index-json.sh now hard-fails without yq/python3")

# ==== P1-4: README platform entry note ====
path5 = 'src/skills/README.md'
with open(path5) as f:
    c = f.read()
c = c.replace(
    'The `skills-index.yaml` file is the machine-readable catalog. Use it to',
    'The `skills-index.yaml` file is the machine-readable catalog. Platform entry files (CLAUDE.md, AGENTS.md, etc.) are currently maintained manually and validated for consistency with the index. A future release will auto-generate them. Use the catalog to'
)
with open(path5, 'w') as f:
    f.write(c)
print("P1-4: README platform entry note added")

# ==== P2-1: validator Check 16 (duplicate detection) ====
path6 = 'src/skills/scripts/validate-skills-strict.sh'
with open(path6) as f:
    c = f.read()
new_check = """

# --- Check 16: Duplicate source_anchors/verified_by entries ---
echo ""
echo "--- Check 16: Duplicate entries in Front Matter lists ---"
python3 -c "
import os, glob
skills_dir = os.environ.get('SKILLS_DIR', 'src/skills')
for sf in sorted(glob.glob(os.path.join(skills_dir, '*/SKILL.md'))):
    name = os.path.basename(os.path.dirname(sf))
    with open(sf) as f:
        lines = f.readlines()
    seen = {}; dup = []
    for l in lines:
        if l.startswith('  - '):
            if l in seen: dup.append(l.strip())
            seen[l] = 1
    if dup:
        print(f'  WARNING: [{name}] Duplicate entries: {dup}')
" 2>/dev/null || true
"""
# Insert before Summary
c = c.replace('\n# --- Summary ---', new_check + '\n# --- Summary ---')
with open(path6, 'w') as f:
    f.write(c)
print("P2-1: Check 16 added (duplicate detection)")

# ==== P2-2: theme-component-system capability table ====
path7 = 'src/skills/theme-component-system/SKILL.md'
with open(path7) as f:
    c = f.read()
cap_table = """## Capability Status

| Capability | Status |
|---|---|
| theme.yaml V2 manifest parsing | beta |
| section/component rendering | beta |
| section schema validation | beta |
| theme-catalog.json export | planned |
| Page Composer | planned |
| data binding auto-resolve | beta |
| theme inheritance chains | beta |

"""
# Insert after the Overview section (after "Key concepts:")
insert_after = "and empowers the Page Composer to assemble pages from modular building blocks."
if insert_after in c:
    c = c.replace(insert_after + '\n', insert_after + '\n\n' + cap_table)
with open(path7, 'w') as f:
    f.write(c)
print("P2-2: theme-component-system capability table added")

# ==== P2-3: webhook start note ====
path8 = 'src/skills/bukit-cli-reference/SKILL.md'
with open(path8) as f:
    c = f.read()
c = c.replace(
    '| `webhook` | Webhook server (Notion trigger → GitHub repository_dispatch) |',
    '| `webhook` | Webhook server (Notion trigger → GitHub repository_dispatch) — note: `start` arg in help is not CLI-registered |'
)
with open(path8, 'w') as f:
    f.write(c)
print("P2-3: webhook start note added")

print("\nAll round 4 fixes applied")
