#!/usr/bin/env python3
"""Apply all round 3 fixes: P0 inline parsing, P1 infra, P2 semantics"""
import os

# ============================================================
# P0: Fix check-cli-commands.py inline subcommand parsing
# ============================================================
path = 'src/skills/scripts/check-cli-commands.py'
with open(path) as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    new_lines.append(line)
    stripped = line.strip()
    if 'new CliCommandSpec(' in stripped and 'Name:' in stripped:
        # Inline Name detection
        m = __import__('re').search(r'Name:\s*"([^"]+)"', stripped)
        if m:
            cmd = m.group(1)
            if cmd not in ('dir', 'n', 'name', 'port', 'ratio', 'file', 'output'):
                # Insert inline command handling before the var continue
                insert = f'\n    # Inline command detection\n    _inline_m = __import__("re").search(r\'Name:\\\\s*"([^"]+)"\', stripped)\n    if _inline_m:\n        _inline_cmd = _inline_m.group(1)\n        if is_command_name(_inline_cmd):\n            if in_subcommands and parent_name:\n                source_commands.add(f\'{{parent_name}} {{_inline_cmd}}\')\n            elif not in_subcommands:\n                source_commands.add(_inline_cmd)\n'
                pass  # Skip this complex approach
    new_lines.append(line)

# Actually, let me just rewrite the critical section of the script more cleanly
# The inline fix should go in the for loop before the 'continue'

with open(path) as f:
    content = f.read()

old_block = """if 'new CliCommandSpec(' in stripped:
        m = re.match(r'var\\s+(\\w+)\\s*=', stripped)
        if m:
            parent_name = m.group(1)
            in_subcommands = 0
        continue"""

new_block = """if 'new CliCommandSpec(' in stripped:
        # Check for inline Name before var declaration
        m_name = re.search(r'Name:\\s*"([^"]+)"', stripped)
        if m_name:
            cmd_name_inline = m_name.group(1)
            if is_command_name(cmd_name_inline):
                if in_subcommands and parent_name:
                    source_commands.add(f'{parent_name} {cmd_name_inline}')
                elif not in_subcommands:
                    source_commands.add(cmd_name_inline)
        # Then handle var declaration
        m = re.match(r'var\\s+(\\w+)\\s*=', stripped)
        if m:
            parent_name = m.group(1)
            in_subcommands = 0
        continue"""

content = content.replace(old_block, new_block)
with open(path, 'w') as f:
    f.write(content)
print("P0: Fixed inline subcommand parsing")

# ============================================================
# P1: export REPO_ROOT + SKILLS_DIR
# ============================================================
path2 = 'src/skills/scripts/validate-skills-strict.sh'
with open(path2) as f:
    content = f.read()
content = content.replace(
    'REPO_ROOT="$(cd "$SKILLS_DIR/../.." && pwd)"',
    'REPO_ROOT="$(cd "$SKILLS_DIR/../.." && pwd)"\nexport SKILLS_DIR\nexport REPO_ROOT'
)
with open(path2, 'w') as f:
    f.write(content)
print("P1: Exported REPO_ROOT and SKILLS_DIR")

# ============================================================
# P1: README fixes
# ============================================================
path3 = 'src/skills/README.md'
with open(path3) as f:
    content = f.read()

# 3.1 File Layout
content = content.replace(
    '├── using-bukit/SKILL.md         ← Gateway: routes to all sub-skills\n├── bukit-*/SKILL.md             ← 19 domain skills (CLI, config, theme, templates, …)',
    '├── using-bukit/SKILL.md           ← Gateway skill\n├── bukit-*/SKILL.md               ← Bukit domain skills\n├── theme-component-system/SKILL.md ← V2 componentized theme skill'
)

# 3.3 GEO 7→10
content = content.replace('geo audit with GEO Score (7 diagnostic codes)', 'geo audit with GEO Score (10 diagnostic codes)')

# 3.2 CI Verification
content = content.replace(
    '### CI Verification\n\n```bash\n# Validate all skill files\nbash src/skills/scripts/validate-skills.sh\n\n# Regenerate JSON index after YAML changes\nbash src/skills/scripts/generate-index-json.sh\n```\n\nThe validate script checks:\n- Front Matter completeness (`name` + `description`)\n- `description` starts with "Use when…"\n- Multilingual Triggers section present\n- Common Errors section present\n- No hardcoded platform-specific tool names\n- `plugin.json` paths all resolve to existing files\n- `skills-index.yaml` entries match existing SKILL.md files',
    '### CI Verification\n\n```bash\n# Basic validation (format, triggers, common errors)\nbash src/skills/scripts/validate-skills.sh\n\n# Strict validation (15 semantic checks — see below)\nbash src/skills/scripts/validate-skills-strict.sh\n\n# Regenerate JSON index after YAML changes\nbash src/skills/scripts/generate-index-json.sh\n```\n\nThe strict validator runs 15 checks: skill count, plugin.json sync, Front Matter completeness, source_anchors paths, guide_chapters paths, local absolute paths, platform tool names, JSON sync, requires dependencies, workflow chains, Markdown table consistency, CLI commands consistency, status consistency, YAML code block validation, status keyword consistency.\n\nThe basic validate script checks:\n- Front Matter completeness (`name` + `description`)\n- `description` starts with "Use when…"\n- Multilingual Triggers section present\n- Common Errors section present\n- No hardcoded platform-specific tool names\n- `plugin.json` paths all resolve to existing files\n- `skills-index.yaml` entries match existing SKILL.md files'
)
with open(path3, 'w') as f:
    f.write(content)
print("P1: README fixed (Layout, CI, GEO)")

# ============================================================
# P1: QUALITY_REPORT.md — add inline subcommand entry
# ============================================================
path4 = 'src/skills/QUALITY_REPORT.md'
with open(path4) as f:
    content = f.read()
content = content.replace(
    '| check-cli-commands.py does not parse full command paths | Fixed — parses parent.child paths with whitelist |',
    '| check-cli-commands.py does not parse full command paths | Fixed — parses parent.child paths with whitelist |\n| check-cli-commands.py inline subcommand parsing | Remaining — inline Name: detection needs hardening |'
)
with open(path4, 'w') as f:
    f.write(content)
print("P1: QUALITY_REPORT updated")

# ============================================================
# P1: generate-index-json.sh CI mode
# ============================================================
path5 = 'src/skills/scripts/generate-index-json.sh'
with open(path5) as f:
    content = f.read()
content = content.replace(
    "    print('Install PyYAML: pip3 install pyyaml')\n    print('Or install yq: brew install yq')\n    exit(0)",
    "    print('Install PyYAML: pip3 install pyyaml')\n    print('Or install yq: brew install yq')\n    if os.environ.get('CI', '') == 'true':\n        print('ERROR: yq and PyYAML not available in CI — refusing to skip JSON generation', file=sys.stderr)\n        sys.exit(1)\n    exit(0)"
)
# Need to add sys import check
content = content.replace('import json', 'import json, sys, os')
with open(path5, 'w') as f:
    f.write(content)
print("P1: generate-index-json.sh CI mode added")

# ============================================================
# P2: using-bukit tone
# ============================================================
path6 = 'src/skills/using-bukit/SKILL.md'
with open(path6) as f:
    content = f.read()
content = content.replace(
    'IF THE USER MENTIONS BUKIT, YOU HAVE NO CHOICE. BUKIT SKILLS ARE THE ONLY SKILLS FOR THIS TASK.\n\nThis is not negotiable.',
    'IF THE USER REQUESTS A BUKIT IMPLEMENTATION TASK, Bukit skills take priority over other SSG tools. For comparison, migration, or architecture analysis that mentions Bukit alongside other tools, load Bukit skills as primary context while allowing other tool knowledge for contrast.'
)
with open(path6, 'w') as f:
    f.write(content)
print("P2: using-bukit tone adjusted")

# ============================================================
# P2: theme-component-system replaces→extends
# ============================================================
path7 = 'src/skills/theme-component-system/SKILL.md'
with open(path7) as f:
    content = f.read()
content = content.replace(
    "is Bukit's next-generation theme architecture that replaces the flat `theme.yaml` V1 format. Instead of a simple name/version/params structure, a V2 theme defines",
    "is Bukit's beta componentized theme architecture. It extends and coexists with the flat `theme.yaml` V1 format, adding a structured approach where themes define"
)
with open(path7, 'w') as f:
    f.write(content)
print("P2: theme-component-system replaces→extends")

# ============================================================
# P2: docstring update
# ============================================================
path8 = 'src/skills/scripts/check-markdown-tables.py'
with open(path8) as f:
    content = f.read()
content = content.replace(
    '"""Check for markdown table issues: merged rows, column count, duplicate commands"""',
    '"""Check for markdown table issues: merged rows, column counts, duplicate commands, table consistency"""'
)
with open(path8, 'w') as f:
    f.write(content)
print("P2: docstring updated")

print("\nAll round 3 fixes applied")
