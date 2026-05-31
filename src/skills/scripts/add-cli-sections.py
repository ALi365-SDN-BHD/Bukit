#!/usr/bin/env python3
path = 'src/skills/bukit-cli-reference/SKILL.md'
with open(path) as f:
    lines = f.readlines()

# Find "## Exit Codes" line and insert before it
insert_at = None
for i, line in enumerate(lines):
    if line.strip().startswith('## Exit Codes'):
        insert_at = i
        break

if insert_at is None:
    print("ERROR: Could not find ## Exit Codes")
    exit(1)

block = [
    '\n',
    '### clone (beta)\n',
    '\n',
    'Generate Bukit theme and content by extracting data from a target website.\n',
    'Requires design token JSON files (typically produced by Browser MCP extraction).\n',
    '\n',
    '```\n',
    'bukit clone --tokens <file> --theme <name> [options]\n',
    '```\n',
    '\n',
    '| Parameter | Default | Description |\n',
    '|------|--------|------|\n',
    '| `--tokens` | - | Design tokens JSON file (required) |\n',
    '| `--theme` | - | Target theme name (required) |\n',
    '| `--layout` | - | Page layout JSON file |\n',
    '| `--page` | - | Page metadata JSON file |\n',
    '| `--sections` | - | Page sections JSON file |\n',
    '| `--behaviors` | - | Interaction behavior JSON file |\n',
    '| `--icons` | - | SVG icons JSON file |\n',
    '| `--assets` | - | Static assets JSON file (auto-downloads images) |\n',
    '| `--brand` | - | Brand name for navbar and footer |\n',
    '| `--use` | false | Switch to this theme after creation |\n',
    '| `--force` | false | Overwrite existing theme |\n',
    '| `--verify` | false | Run doctor/build verification after generation |\n',
    '| `--visual-threshold` | - | Visual screenshot diff threshold (0-1) |\n',
    '| `--fail-on-visual-diff` | false | Fail when screenshot diff exceeds threshold |\n',
    '| `--fidelity` | - | Fidelity mode: directly migrate HTML directory as templates |\n',
    '| `--config` | site.yaml | Config file path |\n',
    '| `--site` | - | Multi-site name |\n',
    '\n',
    'Two modes:\n',
    '- Standard mode: Requires --tokens. Flow: design tokens to theme generation to optional verification.\n',
    '- Fidelity mode (--fidelity): Directly migrates a directory of HTML files as Scriban templates.\n',
    '\n',
    'See bukit-clone skill for the full Browser MCP extraction workflow.\n',
    '\n',
    '### route inspect\n',
    '\n',
    'List all routes with optional JSON output and collection filtering.\n',
    '\n',
    '```\n',
    'bukit route inspect [--json] [--collection <name>] [--config <path>] [--site <name>]\n',
    '```\n',
    '\n',
    '| Parameter | Default | Description |\n',
    '|------|--------|------|\n',
    '| `--json` | false | Output in JSON format |\n',
    '| `--collection` | - | Filter by collection name |\n',
    '| `--config` | site.yaml | Config file path |\n',
    '| `--site` | - | Multi-site name |\n',
    '\n',
    '### docs check (beta)\n',
    '\n',
    'Check consistency between README, user guide, skills documentation, and source code.\n',
    '\n',
    '```\n',
    'bukit docs check [--cli] [--config-fields] [--file-refs] [--examples] [--skills]\n',
    '```\n',
    '\n',
    '| Parameter | Default | Description |\n',
    '|------|--------|------|\n',
    '| `--cli` | false | Check CLI command coverage in docs |\n',
    '| `--config-fields` | false | Check site.yaml field references |\n',
    '| `--file-refs` | false | Check file path reference validity |\n',
    '| `--examples` | false | Check README example parseability |\n',
    '| `--skills` | false | Check Skill-CLI consistency |\n',
    '\n',
    'Without any flags, all checks are performed. Use individual flags to limit scope.\n',
    '\n',
]

for i, line in enumerate(block):
    lines.insert(insert_at + i, line)

with open(path, 'w') as f:
    f.writelines(lines)
print("Done: CLI detailed sections added")
