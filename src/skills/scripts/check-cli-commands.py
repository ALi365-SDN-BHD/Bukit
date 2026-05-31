#!/usr/bin/env python3
"""Check CLI commands in reference against source BukitCliSpecs.cs"""
import re, os, sys

repo_root = os.environ.get('REPO_ROOT', os.getcwd())

specs_path = os.path.join(repo_root, 'src', 'Bukit.Cli', 'Cli', 'BukitCliSpecs.cs')
cli_commands = set()
if os.path.exists(specs_path):
    with open(specs_path) as f:
        for line in f:
            m = re.search(r'Name:\s*"([^"]+)"', line)
            if m:
                cli_commands.add(m.group(1))
    print(f'  Source has {len(cli_commands)} registered commands')
else:
    print(f'  Warning: {specs_path} not found')
    sys.exit(0)

ref_path = os.path.join(repo_root, 'src', 'skills', 'bukit-cli-reference', 'SKILL.md')
ref_commands = set()
if os.path.exists(ref_path):
    with open(ref_path) as f:
        for line in f:
            m = re.match(r'\| `([^`]+)` \|', line)
            if m:
                cmd = m.group(1).split(' (')[0]
                ref_commands.add(cmd)
    print(f'  CLI reference has {len(ref_commands)} documented commands')

source_only = sorted(cli_commands - ref_commands)
ref_only = sorted(ref_commands - cli_commands)
if source_only:
    print(f'  Commands in source but NOT in CLI reference: {source_only}')
if ref_only:
    print(f'  Commands in CLI reference but NOT in source: {ref_only}')
if not source_only and not ref_only:
    print('  All CLI commands consistent between source and reference')
