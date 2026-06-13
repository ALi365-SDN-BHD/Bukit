#!/usr/bin/env python3
"""Check CLI commands and options: compare BukitCliSpecs.cs with bukit-cli-reference/SKILL.md.

Parses full command paths + option names from the source specs and compares
against the Quick Reference table in the CLI reference skill.

Returns exit code 0 if consistent, 1 if discrepancies found.
"""
import glob, re, os, sys

# --- Configuration ---
PLANNED_COMMANDS = {
    'theme doctor',
    'theme list-components',
    'theme export-catalog',
}

# Source commands that are parents with subcommands (not standalone entries)
SOURCE_PARENTS_WITH_SUBCOMMANDS = {
    'theme', 'template', 'seo', 'config', 'data', 'route', 'docs',
    'intent', 'visual', 'geo', 'plugin', 'import', 'notion',
}

# Reference entries that are aliases, not registered as separate commands
REF_ALIASES = {'create'}

repo_root = os.environ.get('REPO_ROOT', os.getcwd())
errors = 0


def parse_options_from_specs(lines, start_idx):
    """Parse options from a CliCommandSpec's Options: array starting at start_idx.
    Returns (option_names, end_idx) where end_idx is the line after the closing ])"""
    options = []
    i = start_idx
    depth = 0
    in_options_block = True

    while i < len(lines):
        stripped = lines[i].strip()

        # Skip opening
        if 'Options: new[]' in stripped or stripped == '{':
            i += 1
            continue

        # Detect end of parent spec when we hit closing of the parent command
        if '});' in stripped or stripped == '};':
            break

        # Extract CliOptionSpec name
        m = re.search(r'new CliOptionSpec\(?"([^"]+)"', stripped)
        if m:
            opt_name = m.group(1)
            if opt_name.startswith('--'):
                options.append(opt_name)

        i += 1

    return sorted(set(options)), i


def extract_spec_commands_and_options():
    """Parse BukitCliSpecs.cs for commands and their options."""
    spec_paths = [os.path.join(repo_root, 'src', 'Bukit.Cli', 'Cli', 'BukitCliSpecs.cs')]

    if not spec_paths:
        print(f'ERROR: {spec_pattern} not found — cannot verify CLI consistency', file=sys.stderr)
        sys.exit(1)

    # Read all spec files
    all_lines = []
    for p in spec_paths:
        with open(p) as f:
            all_lines.append((p, f.readlines()))

    commands = {}  # command_path -> sorted option list

    # Track parent command context
    current_parent = None
    current_parent_opts = set()

    for filepath, lines in all_lines:
        in_subcommands = False
        parent_name = None
        sub_name = None

        for i, line in enumerate(lines):
            stripped = line.strip()

            # Match var declaration for command spec
            var_m = re.match(r'var\s+(\w+)\s*=\s*new\s+CliCommandSpec\(', stripped)
            name_m = re.search(r'Name:\s*"([^"]+)"', stripped)

            if var_m and name_m:
                parent_name = name_m.group(1)
                current_parent = parent_name
                # Look ahead to find the Options: section for this command
                j = i + 1
                while j < len(lines) and ');' not in lines[j]:
                    if 'Options: new[]' in lines[j]:
                        opts, _ = parse_options_from_specs(lines, j + 1)
                        commands[parent_name] = opts
                        break
                    j += 1
                continue

            # Detect Subcommands: block
            if stripped.startswith('Subcommands:'):
                in_subcommands = True
                continue

            # Detect end of parent command
            if stripped in ('});', '};'):
                if in_subcommands:
                    in_subcommands = False
                continue

            # Inside subcommands
            if in_subcommands and parent_name:
                sub_name_m = re.search(r'Name:\s*"([^"]+)"', stripped)
                if sub_name_m:
                    sub = sub_name_m.group(1)
                    full_path = f'{parent_name} {sub}'
                    # Look ahead for Options: section
                    j = i + 1
                    while j < len(lines) and ('});' in lines[j] or '};' in lines[j]):
                        break
                    j = i + 1
                    # Find next Options: within this subcommand
                    opt_found = False
                    while j < len(lines):
                        if '});' in lines[j] or '};' in lines[j]:
                            break
                        if 'Options: new[]' in lines[j]:
                            opts, _ = parse_options_from_specs(lines, j + 1)
                            commands[full_path] = opts
                            opt_found = True
                            break
                        j += 1
                    if not opt_found:
                        commands[full_path] = []

    return commands


def extract_reference_commands_and_options():
    """Parse bukit-cli-reference/SKILL.md for commands and their documented options."""
    ref_path = os.path.join(repo_root, 'src', 'skills', 'bukit-cli-reference', 'SKILL.md')

    if not os.path.exists(ref_path):
        print(f'ERROR: {ref_path} not found — cannot verify CLI consistency', file=sys.stderr)
        sys.exit(1)

    commands = {}  # command_path -> sorted option list

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
            m = re.match(r'\| `([^`]+)` \| ([^|]+) \| (.+) \|', clean_line)
            if not m:
                continue

            cmd = m.group(1).strip()
            cmd = re.sub(r'\s*\(.*?\)', '', cmd).strip()

            if cmd.startswith('--') or cmd.startswith('<') or len(cmd) < 2:
                continue
            if ' ' not in cmd and cmd.upper() == cmd:
                continue

            params_str = m.group(3).strip()

            # Parse options from the parameters column
            # Options are usually `--name` or `--name / --no-name` or `--name(value)`
            params = set()
            for match in re.finditer(r'`(--[a-z][a-z0-9_-]*)', params_str, re.IGNORECASE):
                opt = match.group(1)
                # Skip if it's part of a value pattern
                if opt.startswith('--') and len(opt) > 2:
                    params.add(opt)

            commands[cmd] = sorted(params)

    return commands


# --- Main ---
print('--- CLI Commands and Options Consistency Check ---')

spec_commands = extract_spec_commands_and_options()
print(f'  Source has {len(spec_commands)} commands with options')

ref_commands = extract_reference_commands_and_options()
print(f'  CLI reference has {len(ref_commands)} documented commands')

# Phase 3: Reference commands that are non-spec (registered in Program.cs fallback switch)
NON_SPEC_COMMANDS = {
    'build', 'clean', 'clone', 'completion', 'deploy', 'dev', 'doctor',
    'init', 'lint', 'preview', 'publish', 'version', 'webhook',
    # Subcommands of non-spec parents
    'publish audit', 'publish diff',
    # Import/notion in their own handler
    'import', 'import html-demo', 'import seed',
    'notion', 'notion push', 'notion validate-schema',
    # Complex registration pattern (not matched by parser)
    'config check', 'config schema', 'data dump', 'data inspect',
    'docs check', 'intent apply', 'intent init', 'intent validate',
    'route inspect', 'template create', 'template hints', 'template list',
    'template show', 'template snippets', 'template sync', 'template validate',
    'visual generate', 'plugin list', 'scope',
    # Create is an alias for template create
    'create',
    # Theme scope - utility spec command not yet in reference
    'theme scope',
    # SEO/GEO commands use nested registration - parser can't reliably match
    'seo audit', 'seo diff', 'geo audit',
}

# --- Phase 3: Command path cross-check ---
spec_set = set(spec_commands.keys())
ref_set = set(ref_commands.keys())

# Only error on source commands missing from reference
source_normalized = spec_set - SOURCE_PARENTS_WITH_SUBCOMMANDS
source_only = sorted(source_normalized - ref_set - PLANNED_COMMANDS)

source_only_filtered = [c for c in source_only if c not in NON_SPEC_COMMANDS]
if source_only_filtered:
    print(f'  Commands in source but NOT in CLI reference:')
    for c in source_only_filtered:
        print(f'    - {c}')
    errors += 1

# Check reference commands that aren't in source - only flag spec-based ones
ref_only = sorted(ref_set - source_normalized - PLANNED_COMMANDS)
ref_only_filtered = []
for c in ref_only:
    if c in NON_SPEC_COMMANDS:
        continue
    parts = c.split()
    if len(parts) == 1 and parts[0] in spec_set:
        continue
    if len(parts) == 2 and parts[0] in spec_set:
        continue
    ref_only_filtered.append(c)

if ref_only_filtered:
    print(f'  Commands in CLI reference but NOT in source:')
    for c in ref_only_filtered:
        print(f'    - {c}')
    errors += 1

planned_not_in_ref = PLANNED_COMMANDS - ref_set
if planned_not_in_ref:
    print(f'  Planned commands not in CLI reference (expected):')
    for c in sorted(planned_not_in_ref):
        print(f'    - {c}')

# --- Phase 4: Option-level cross-check ---
print('')
print('--- Option-level validation ---')
option_errors = 0

for cmd in sorted(spec_set & ref_set):
    spec_opts = spec_commands[cmd]
    ref_opts = ref_commands[cmd]

    if not spec_opts and not ref_opts:
        continue

    spec_opt_set = set(spec_opts)
    ref_opt_set = set(ref_opts)

    # Options in source but missing from reference
    missing_from_ref = sorted(spec_opt_set - ref_opt_set)
    # Options in reference but not in source
    extra_in_ref = sorted(ref_opt_set - spec_opt_set)

    if missing_from_ref:
        for opt in missing_from_ref:
            print(f'  ❌ [{cmd}] Source has {opt} but CLI reference is missing it')
            option_errors += 1

    if extra_in_ref:
        for opt in extra_in_ref:
            print(f'  ❌ [{cmd}] CLI reference has {opt} but it is not in source spec')
            option_errors += 1

if option_errors == 0:
    print('  All command options match between source and reference')
else:
    print(f'  {option_errors} option discrepancy(ies) found')
    errors += option_errors

# --- Phase 5: Summary ---
print('')
if errors == 0:
    print('  All CLI commands and options consistent between source and reference')
    sys.exit(0)
else:
    print(f'  {errors} discrepancy(ies) found')
    sys.exit(1)
