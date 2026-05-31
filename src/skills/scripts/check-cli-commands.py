#!/usr/bin/env python3
"""Check CLI commands: compare BukitCliSpecs.cs with bukit-cli-reference/SKILL.md

Parses full command paths (parent.child) from the source and compares against
the Quick Reference table in the CLI reference skill.

Returns exit code 0 if consistent, 1 if discrepancies found.
"""
import re, os, sys

# --- Configuration ---
PLANNED_COMMANDS = {
    'theme doctor',
    'theme list-components',
    'theme export-catalog',
}

# Source commands that are intentionally NOT in the table as standalone entries
# (they are covered by subcommand entries or are implementation details)
SOURCE_PARENTS_WITH_SUBCOMMANDS = {
    'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',
    'intent', 'visual', 'geo', 'plugin',
}

# Reference entries that are aliases, not registered as separate commands
REF_ALIASES = {'create'}  # 'create' is alias for 'init'

repo_root = os.environ.get('REPO_ROOT', os.getcwd())

# --- Phase 1: Parse BukitCliSpecs.cs for full command paths ---
specs_path = os.path.join(repo_root, 'src', 'Bukit.Cli', 'Cli', 'BukitCliSpecs.cs')
source_commands = set()

if not os.path.exists(specs_path):
    print(f'ERROR: {specs_path} not found — cannot verify CLI consistency', file=sys.stderr)
    sys.exit(1)

with open(specs_path) as f:
    lines = f.readlines()

parent_name = None
in_subcommands = 0

def is_command_name(name):
    """Filter out parameter/option names"""
    if name.startswith('--'):
        return False
    if name in ('dir', 'n', 'name', 'port', 'ratio', 'file', 'output',
                'host', 'site', 'config', 'out', 'shell', 'repo', 'event',
                'path', 'force', 'brand', 'theme', 'layout', 'page',
                'sections', 'behaviors', 'icons', 'assets', 'tokens',
                'message', 'collection', 'strict', 'external', 'json',
                'report', 'baseline', 'current', 'visual-threshold',
                'fidelity', 'verify', 'skills', 'cli', 'config-fields',
                'file-refs', 'examples', 'refresh', 'registry', 'registry-url',
                'from', 'primary-color', 'accent-color', 'site-url',
                'base-url', 'cache-dir', 'metrics', 'jobs', 'log-format',
                'no-watch', 'draft', 'incremental', 'no-incremental',
                'strict-port', 'ci', 'no-clean', 'dry-run',
                'skip-build', 'max-new-errors', 'max-new-warnings',
                'max-new-issues', 'fail-on-new-code', 'fail-on-route-removed',
                'fail-on-indexable-drop', 'fail-on-visual-diff',
                'module', 'format', 'root-dir'):
        return False
    return True

for line in lines:
    stripped = line.strip()

    if 'new CliCommandSpec(' in stripped:
        # Check for inline Name before var declaration
        m_name = re.search(r'Name:\s*"([^"]+)"', stripped)
        if m_name:
            cmd_name_inline = m_name.group(1)
            if is_command_name(cmd_name_inline):
                if in_subcommands and parent_name:
                    source_commands.add(f'{parent_name} {cmd_name_inline}')
                elif not in_subcommands:
                    source_commands.add(cmd_name_inline)
        # Then handle var declaration
        m = re.match(r'var\s+(\w+)\s*=', stripped)
        if m:
            parent_name = m.group(1)
            in_subcommands = 0
        continue

    if stripped.startswith('Subcommands:'):
        in_subcommands = 1
        continue

    if in_subcommands and (stripped.startswith('});') or stripped == '};'):
        in_subcommands = 0
        continue

    m = re.search(r'Name:\s*"([^"]+)"', stripped)
    if not m:
        continue
    cmd_name = m.group(1)

    if not is_command_name(cmd_name):
        continue

    if in_subcommands and parent_name:
        source_commands.add(f'{parent_name} {cmd_name}')
    elif not in_subcommands:
        source_commands.add(cmd_name)

print(f'  Source has {len(source_commands)} commands (full paths)')

# --- Phase 2: Parse CLI reference Quick Reference table ---
ref_path = os.path.join(repo_root, 'src', 'skills', 'bukit-cli-reference', 'SKILL.md')
ref_commands = set()

if not os.path.exists(ref_path):
    print(f'ERROR: {ref_path} not found — cannot verify CLI consistency', file=sys.stderr)
    sys.exit(1)

with open(ref_path) as f:
    in_quick_ref = False
    for line in f:
        stripped = line.strip()
        
        if stripped == '| Command | Purpose | Key Parameters |':
            in_quick_ref = True
            continue
        
        if in_quick_ref and not stripped.startswith('|'):
            in_quick_ref = False
            continue
        
        if not in_quick_ref:
            continue
        
        clean_line = re.sub(r'\s*\([^)]*\)', '', stripped)
        m = re.match(r'\| `([^`]+)` \|', clean_line)
        if not m:
            continue
        
        cmd = m.group(1).strip()
        cmd = re.sub(r'\s*\(.*?\)', '', cmd).strip()
        
        if cmd.startswith('--') or cmd.startswith('<') or len(cmd) < 2:
            continue
        
        if ' ' not in cmd and cmd.upper() == cmd:
            continue
        
        ref_commands.add(cmd)

print(f'  CLI reference has {len(ref_commands)} documented commands')

# --- Phase 3: Cross-check ---
# Normalize: remove parent-only entries from source (they have subcommands listed separately)
source_normalized = source_commands - SOURCE_PARENTS_WITH_SUBCOMMANDS
ref_normalized = ref_commands - REF_ALIASES

source_only = sorted(source_normalized - ref_normalized - PLANNED_COMMANDS)
ref_only = sorted(ref_normalized - source_normalized - PLANNED_COMMANDS)

errors = 0
if source_only:
    print(f'  Commands in source but NOT in CLI reference:')
    for c in source_only:
        print(f'    - {c}')
    errors += 1

if ref_only:
    print(f'  Commands in CLI reference but NOT in source:')
    for c in ref_only:
        print(f'    - {c}')
    errors += 1

planned_in_ref = ref_commands & PLANNED_COMMANDS
planned_not_in_ref = PLANNED_COMMANDS - ref_commands
if planned_not_in_ref:
    print(f'  Planned commands not in CLI reference (expected if marked as planned sections):')
    for c in sorted(planned_not_in_ref):
        print(f'    - {c}')

if errors == 0:
    print('  All CLI commands consistent between source and reference')
    sys.exit(0)
else:
    print(f'  {errors} discrepancy category(ies) found')
    sys.exit(1)
